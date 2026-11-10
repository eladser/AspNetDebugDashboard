using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text;
using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AspNetDebugDashboard.Tests;

// A minimal loopback HTTP server standing in for "some external API". Real sockets are
// required here: DiagnosticListener's HttpRequestOut events only fire for actual network
// sends, not for TestServer's in-memory transport.
internal sealed class FakeUpstream : IDisposable
{
    private readonly HttpListener _listener = new();
    public string BaseUrl { get; }

    public Func<HttpListenerRequest, (int status, string body)> Handler { get; set; }
        = _ => (200, "ok");

    public FakeUpstream()
    {
        var port = GetFreePort();
        BaseUrl = $"http://localhost:{port}/";
        _listener.Prefixes.Add(BaseUrl);
        _listener.Start();
        _ = ServeLoop();
    }

    private async Task ServeLoop()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch { return; }

            var (status, body) = Handler(ctx.Request);
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json";
            var bytes = Encoding.UTF8.GetBytes(body);
            ctx.Response.ContentLength64 = bytes.Length;
            await ctx.Response.OutputStream.WriteAsync(bytes);
            ctx.Response.Close();
        }
    }

    public static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Dispose() => _listener.Close();
}

public class OutgoingHttpCaptureTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private FakeUpstream _upstream = null!;

    public Task InitializeAsync()
    {
        _upstream = new FakeUpstream();
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseContentRoot(Directory.GetCurrentDirectory());
        });
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        _upstream.Dispose();
        return Task.CompletedTask;
    }

    private IDebugStorage Storage => _factory.Services.GetRequiredService<IDebugStorage>();

    private static async Task WaitUntil(Func<Task<bool>> condition, int timeoutMs = 3000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (await condition()) return;
            await Task.Delay(25);
        }
        Assert.Fail("condition not met in time");
    }

    [Fact]
    public async Task SimpleGet_IsCapturedAndLinkedToParentRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/test-outgoing?url={Uri.EscapeDataString(_upstream.BaseUrl)}");
        response.EnsureSuccessStatusCode();

        PagedResult<OutgoingHttpEntry> calls = null!;
        await WaitUntil(async () =>
        {
            calls = await Storage.GetOutgoingHttpAsync(new DebugFilter { PageSize = 50 });
            return calls.Items.Count > 0;
        });

        var call = calls.Items.Single();
        call.Method.Should().Be("GET");
        call.StatusCode.Should().Be(200);
        call.RequestId.Should().NotBeNullOrEmpty();

        var requests = await Storage.GetRequestsAsync(new DebugFilter { RequestId = call.RequestId, PageSize = 10 });
        requests.Items.Should().ContainSingle(r => r.Path == "/test-outgoing");

        // the stored request carries the call too, which is what the detail view's HTTP tab reads
        var parent = await Storage.GetRequestAsync(requests.Items.Single().Id);
        parent!.OutgoingHttp.Should().ContainSingle(o => o.Method == "GET" && o.StatusCode == 200);
    }

    [Fact]
    public async Task PlainNewHttpClient_IsCaptured()
    {
        // /test-outgoing uses `new HttpClient()` directly, not IHttpClientFactory.
        var client = _factory.CreateClient();
        await client.GetAsync($"/test-outgoing?url={Uri.EscapeDataString(_upstream.BaseUrl)}");

        await WaitUntil(async () => (await Storage.GetOutgoingHttpCountAsync()) > 0);
    }

    [Fact]
    public async Task ExcludedHeadersAndQueryParams_AreRedacted()
    {
        var client = _factory.CreateClient();
        var target = $"{_upstream.BaseUrl}?token=super-secret&keep=me";
        var response = await client.GetAsync($"/test-outgoing?url={Uri.EscapeDataString(target)}&withAuthHeader=true");
        response.EnsureSuccessStatusCode();

        PagedResult<OutgoingHttpEntry> calls = null!;
        await WaitUntil(async () =>
        {
            calls = await Storage.GetOutgoingHttpAsync(new DebugFilter { PageSize = 50 });
            return calls.Items.Count > 0;
        });

        var call = calls.Items.Single();
        call.RequestHeaders.Should().NotContainKey("Authorization");
        call.Url.Should().Contain("token=***");
        call.Url.Should().Contain("keep=me");
    }

    [Fact]
    public async Task UnreachableHost_IsRecordedWithError()
    {
        // A closed loopback port: connection gets actively refused (no artificially short
        // timeout here, so the real HttpRequestException wins the race and the runtime's
        // "System.Net.Http.Exception" event fires with it).
        var deadPort = FakeUpstream.GetFreePort();
        var client = _factory.CreateClient();
        await client.GetAsync($"/test-outgoing?url={Uri.EscapeDataString($"http://127.0.0.1:{deadPort}/")}&timeoutMs=8000");

        PagedResult<OutgoingHttpEntry> calls = null!;
        await WaitUntil(async () =>
        {
            calls = await Storage.GetOutgoingHttpAsync(new DebugFilter { PageSize = 50 });
            return calls.Items.Count > 0;
        }, timeoutMs: 10000);

        var call = calls.Items.Single();
        call.StatusCode.Should().BeNull();
        call.Error.Should().NotBeNullOrEmpty();
        call.ExceptionType.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Bodies_AreCapturedRedactedAndCappedWhenEnabled()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<DebugConfiguration>(cfg =>
                {
                    cfg.CaptureOutgoingHttpBodies = true;
                    cfg.MaxOutgoingBodySize = 60;
                });
            });
        });

        _upstream.Handler = _ => (200, "{\"result\":\"this response body is long enough to get truncated by the cap\"}");

        var client = factory.CreateClient();
        var response = await client.GetAsync($"/test-outgoing?url={Uri.EscapeDataString(_upstream.BaseUrl)}&withBody=true");
        response.EnsureSuccessStatusCode();

        var storage = factory.Services.GetRequiredService<IDebugStorage>();
        PagedResult<OutgoingHttpEntry> calls = null!;
        await WaitUntil(async () =>
        {
            calls = await storage.GetOutgoingHttpAsync(new DebugFilter { PageSize = 50 });
            return calls.Items.Count > 0 && calls.Items[0].ResponseBody != null;
        });

        var call = calls.Items.Single();
        call.RequestBody.Should().Contain("\"username\":\"bob\"");
        call.RequestBody.Should().Contain("\"password\":\"***\"");
        call.ResponseBody.Should().Contain("[truncated]");
    }

    [Fact]
    public async Task OutsideAllowedEnvironments_CapturesNothing()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<DebugConfiguration>(cfg =>
                {
                    cfg.AllowedEnvironments = new List<string> { "Staging" };
                });
            });
        });

        var client = factory.CreateClient();
        await client.GetAsync($"/test-outgoing?url={Uri.EscapeDataString(_upstream.BaseUrl)}");
        await Task.Delay(200);

        var storage = factory.Services.GetRequiredService<IDebugStorage>();
        (await storage.GetOutgoingHttpCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UnattachedCall_IsSkippedByDefault()
    {
        // No inbound request in flight, so DebugContext.Current is null: this is exactly
        // the shape of a background job's or the OTLP exporter's own outgoing call.
        DebugContext.Current.Value = null;
        using var client = new HttpClient();

        try { await client.GetAsync(_upstream.BaseUrl); } catch { /* the point is capture, not this call succeeding */ }
        await Task.Delay(200);

        // Uses the class-level factory's own storage/DebugContext; an unattached call made
        // from the test process should never show up there without opting in.
        (await Storage.GetOutgoingHttpCountAsync()).Should().Be(0);
    }

    // Pins the reflection against the payload shapes the runtime actually emits for
    // HttpRequestOut.Start/Stop, so a future TFM changing them fails loudly here instead of
    // silently in production.
    [Fact]
    public async Task DiagnosticPayloads_ExposeExpectedProperties()
    {
        var probe = new PayloadProbe();
        using var sub = DiagnosticListener.AllListeners.Subscribe(probe);

        using var client = new HttpClient();
        await client.GetAsync(_upstream.BaseUrl);
        await Task.Delay(100);

        probe.StartRequest.Should().NotBeNull();
        probe.StopRequest.Should().NotBeNull();
        probe.StopResponse.Should().NotBeNull();
        probe.StopResponse!.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Subscribes to the real "HttpHandlerDiagnosticListener" and reflects over the payloads
    // the same way OutgoingHttpObserver does, independent of that class's own code.
    private sealed class PayloadProbe : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
    {
        private readonly List<IDisposable> _subs = new();

        public HttpRequestMessage? StartRequest { get; private set; }
        public HttpRequestMessage? StopRequest { get; private set; }
        public HttpResponseMessage? StopResponse { get; private set; }

        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == "HttpHandlerDiagnosticListener") _subs.Add(value.Subscribe(this));
        }

        public void OnNext(KeyValuePair<string, object?> ev)
        {
            switch (ev.Key)
            {
                case "System.Net.Http.HttpRequestOut.Start":
                    StartRequest = GetProp(ev.Value, "Request") as HttpRequestMessage;
                    break;
                case "System.Net.Http.HttpRequestOut.Stop":
                    StopRequest = GetProp(ev.Value, "Request") as HttpRequestMessage;
                    StopResponse = GetProp(ev.Value, "Response") as HttpResponseMessage;
                    break;
            }
        }

        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }

    private static object? GetProp(object? payload, string name) =>
        payload?.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(payload);
}
