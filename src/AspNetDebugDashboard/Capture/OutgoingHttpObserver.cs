using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AspNetDebugDashboard.Capture;

// Listens to the runtime's "HttpHandlerDiagnosticListener", which fires for every
// HttpClient send in the process (new HttpClient(), IHttpClientFactory, SDK clients built
// on top of it) regardless of who created the handler. The event payloads are internal
// anonymous types, so properties are read by reflection - GetProp is pinned by a test on
// every target framework.
public sealed class OutgoingHttpObserver : IHostedService, IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
{
    // Resolved lazily on first capture, not injected directly - IDebugStorage is a lazy
    // singleton, and taking it as a constructor param here would build it (and create the
    // db file) as soon as the host resolves IHostedService, even outside AllowedEnvironments.
    private readonly IServiceProvider _serviceProvider;
    private readonly DebugContext _debugContext;
    private readonly IOptions<DebugConfiguration> _config;
    private readonly IWebHostEnvironment _env;

    private static readonly ConditionalWeakTable<HttpRequestMessage, CallState> State = new();

    private readonly object _gate = new();
    private IDisposable? _allListenersSubscription;
    private readonly List<IDisposable> _listenerSubscriptions = new();

    public OutgoingHttpObserver(IServiceProvider serviceProvider, DebugContext debugContext, IOptions<DebugConfiguration> config, IWebHostEnvironment env)
    {
        _serviceProvider = serviceProvider;
        _debugContext = debugContext;
        _config = config;
        _env = env;
    }

    private IDebugStorage Storage => _serviceProvider.GetRequiredService<IDebugStorage>();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var config = _config.Value;
        if (!DebugDashboardAccess.IsActive(config, _env) || !config.CaptureOutgoingHttp)
            return Task.CompletedTask;

        _allListenersSubscription = DiagnosticListener.AllListeners.Subscribe(this);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Stop new subscriptions from arriving before tearing down the ones already held -
        // AllListeners can still be delivering OnNext on another thread while this runs.
        _allListenersSubscription?.Dispose();

        IDisposable[] subs;
        lock (_gate)
        {
            subs = _listenerSubscriptions.ToArray();
            _listenerSubscriptions.Clear();
        }
        foreach (var sub in subs) sub.Dispose();

        return Task.CompletedTask;
    }

    void IObserver<DiagnosticListener>.OnNext(DiagnosticListener listener)
    {
        if (listener.Name == "HttpHandlerDiagnosticListener")
        {
            var sub = listener.Subscribe(this);
            lock (_gate) _listenerSubscriptions.Add(sub);
        }
    }

    void IObserver<DiagnosticListener>.OnCompleted() { }
    void IObserver<DiagnosticListener>.OnError(Exception error) { }

    void IObserver<KeyValuePair<string, object?>>.OnNext(KeyValuePair<string, object?> ev)
    {
        try
        {
            switch (ev.Key)
            {
                case "System.Net.Http.HttpRequestOut.Start":
                    OnStart(ev.Value);
                    break;
                case "System.Net.Http.HttpRequestOut.Stop":
                    OnStop(ev.Value);
                    break;
                case "System.Net.Http.Exception":
                    OnException(ev.Value);
                    break;
            }
        }
        catch
        {
            // capture must never take the outgoing call down with it
        }
    }

    void IObserver<KeyValuePair<string, object?>>.OnCompleted() { }
    void IObserver<KeyValuePair<string, object?>>.OnError(Exception error) { }

    private void OnStart(object? payload)
    {
        var config = _config.Value;
        if (!config.CaptureOutgoingHttp) return;
        if (GetProp(payload, "Request") is not HttpRequestMessage req) return;

        var scope = DebugContext.Current.Value;
        var uri = req.RequestUri;

        var entry = new OutgoingHttpEntry
        {
            Method = req.Method.Method,
            Url = Redactor.RedactUrl(uri?.ToString() ?? "", config.RedactedBodyFields),
            Host = uri?.Host ?? "",
            RequestHeaders = CollectHeaders(req.Headers, req.Content?.Headers, config.ExcludedHeaders),
            TraceId = scope?.TraceId,
        };

        if (config.CaptureOutgoingHttpBodies && req.Content is ByteArrayContent bac)
        {
            try
            {
                // ByteArrayContent (which StringContent/JsonContent/FormUrlEncodedContent all
                // derive from) is already buffered in memory, so reading it here doesn't
                // consume anything the real send still needs. StreamContent is skipped on
                // purpose: reading it up front would eat the bytes before they go on the wire.
                var body = bac.ReadAsStringAsync().GetAwaiter().GetResult();
                entry.RequestBody = TruncateBody(Redactor.RedactBody(body, config.RedactedBodyFields), config.MaxOutgoingBodySize);
            }
            catch
            {
                // best effort only
            }
        }

        State.AddOrUpdate(req, new CallState(Stopwatch.GetTimestamp(), scope, entry));
    }

    private void OnStop(object? payload)
    {
        if (GetProp(payload, "Request") is not HttpRequestMessage req) return;
        if (!State.TryGetValue(req, out var state)) return;
        State.Remove(req);

        var config = _config.Value;
        if (!config.CaptureOutgoingHttp) return;
        if (!TryResolveRequestId(state.Scope, config, out var requestId)) return;

        var entry = state.Entry;
        entry.RequestId = requestId ?? "";
        entry.ExecutionTimeMs = ElapsedMs(state.StartTimestamp);

        var response = GetProp(payload, "Response") as HttpResponseMessage;
        if (response is not null)
        {
            entry.StatusCode = (int)response.StatusCode;
            entry.ResponseHeaders = CollectHeaders(response.Headers, response.Content?.Headers, config.ExcludedHeaders);
        }
        else if (entry.Error is null)
        {
            // A cancellation or timeout skips "System.Net.Http.Exception" entirely (the
            // runtime only writes it for real faults), so this is the only signal left.
            var status = GetProp(payload, "RequestTaskStatus");
            entry.Error = status is TaskStatus.Canceled ? "Request canceled or timed out" : "Request failed";
        }

        var storeTask = Storage.StoreOutgoingHttpAsync(entry);

        if (!string.IsNullOrEmpty(entry.RequestId))
        {
            _debugContext.AddOutgoingHttp(entry.RequestId, entry);
        }

        if (config.CaptureOutgoingHttpBodies && response?.Content is not null)
        {
            response.Content = new TeeHttpContent(response.Content, config.MaxOutgoingBodySize, body =>
            {
                entry.ResponseBody = Redactor.RedactBody(body, config.RedactedBodyFields);
                _ = PatchResponseBodyAsync(storeTask, entry.ResponseBody);
            });
        }
        else
        {
            _ = SwallowAsync(storeTask);
        }
    }

    private void OnException(object? payload)
    {
        if (GetProp(payload, "Request") is not HttpRequestMessage req) return;
        if (!State.TryGetValue(req, out var state)) return;
        State.Remove(req);

        var config = _config.Value;
        if (!config.CaptureOutgoingHttp) return;
        if (!TryResolveRequestId(state.Scope, config, out var requestId)) return;

        var ex = GetProp(payload, "Exception") as Exception;

        var entry = state.Entry;
        entry.RequestId = requestId ?? "";
        entry.ExecutionTimeMs = ElapsedMs(state.StartTimestamp);
        entry.Error = ex?.Message;
        entry.ExceptionType = ex?.GetType().Name;

        var storeTask = Storage.StoreOutgoingHttpAsync(entry);
        if (!string.IsNullOrEmpty(entry.RequestId))
        {
            _debugContext.AddOutgoingHttp(entry.RequestId, entry);
        }
        _ = SwallowAsync(storeTask);
    }

    // A request only counts as "attached" when this process's DebugContext still recognizes
    // the request id as in flight - it filters out calls left over from a request that
    // already finished, and (since DiagnosticListener is process-wide) calls made by an
    // unrelated host sharing the process, e.g. in tests.
    private bool TryResolveRequestId(RequestScope? scope, DebugConfiguration config, out string? requestId)
    {
        if (scope is not null && _debugContext.GetActiveRequest(scope.RequestId) is not null)
        {
            requestId = scope.RequestId;
            return true;
        }

        requestId = scope?.RequestId;
        return config.CaptureUnattachedOutgoingHttp;
    }

    private async Task PatchResponseBodyAsync(Task<string> storeTask, string body)
    {
        try
        {
            var id = await storeTask.ConfigureAwait(false);
            await Storage.UpdateOutgoingHttpResponseBodyAsync(id, body).ConfigureAwait(false);
        }
        catch
        {
            // never break the response pipeline over a capture failure
        }
    }

    private static async Task SwallowAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // storage errors must never take the outgoing call down with them
        }
    }

    private static Dictionary<string, string> CollectHeaders(System.Net.Http.Headers.HttpHeaders? a, System.Net.Http.Headers.HttpHeaders? b, IReadOnlyCollection<string> excluded)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void Add(System.Net.Http.Headers.HttpHeaders? headers)
        {
            if (headers is null) return;
            foreach (var header in headers)
            {
                if (excluded.Contains(header.Key, StringComparer.OrdinalIgnoreCase)) continue;
                result[header.Key] = string.Join(", ", header.Value);
            }
        }

        Add(a);
        Add(b);
        return result;
    }

    private static string TruncateBody(string body, int maxBytes)
    {
        var bytes = System.Text.Encoding.UTF8.GetByteCount(body);
        if (bytes <= maxBytes) return body;

        var buffer = System.Text.Encoding.UTF8.GetBytes(body);
        return System.Text.Encoding.UTF8.GetString(buffer, 0, maxBytes) + "\n...[truncated]";
    }

    private static long ElapsedMs(long startTimestamp) =>
        (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

    private static object? GetProp(object? payload, string name) =>
        payload?.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(payload);

    private sealed class CallState
    {
        public CallState(long startTimestamp, RequestScope? scope, OutgoingHttpEntry entry)
        {
            StartTimestamp = startTimestamp;
            Scope = scope;
            Entry = entry;
        }

        public long StartTimestamp { get; }
        public RequestScope? Scope { get; }
        public OutgoingHttpEntry Entry { get; }
    }
}
