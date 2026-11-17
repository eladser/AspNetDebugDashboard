using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;

namespace AspNetDebugDashboard.Middleware;

public class DebugRequestMiddleware
{
    private readonly RequestDelegate _next;
    private readonly DebugConfiguration _config;
    private readonly IDebugStorage _storage;
    private readonly DebugContext _debugContext;

    public DebugRequestMiddleware(RequestDelegate next, IOptions<DebugConfiguration> config, IDebugStorage storage, DebugContext debugContext)
    {
        _next = next;
        _config = config.Value;
        _storage = storage;
        _debugContext = debugContext;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_config.IsEnabled || ShouldSkipRequest(context))
        {
            await _next(context);
            return;
        }

        var start = Stopwatch.GetTimestamp();
        var requestId = context.TraceIdentifier;
        var requestBody = "";
        var responseBody = "";
        var elapsed = TimeSpan.Zero;

        // Register the request so the EF interceptor and IDebugLogger can
        // attach queries/logs to it while it's in flight.
        _debugContext.StartRequest(requestId, new RequestEntry { RequestId = requestId });
        DebugContext.Current.Value = new RequestScope(requestId, Activity.Current?.TraceId.ToHexString()) { StartTimestamp = start };

        try
        {
            // Capture request body if configured
            if (_config.LogRequestBodies && context.Request.ContentLength > 0)
            {
                requestBody = await ReadRequestBodyAsync(context);
            }

            // Capture response body if configured
            if (_config.LogResponseBodies)
            {
                var originalBodyStream = context.Response.Body;
                var capture = new CaptureStream(originalBodyStream, context.Response);
                context.Response.Body = capture;

                try
                {
                    await _next(context);
                }
                finally
                {
                    context.Response.Body = originalBodyStream;
                }

                elapsed = Stopwatch.GetElapsedTime(start);
                responseBody = await ReadResponseBodyAsync(capture, originalBodyStream);
            }
            else
            {
                await _next(context);
                elapsed = Stopwatch.GetElapsedTime(start);
            }

            // Log the request
            await LogRequestAsync(context, requestId, requestBody, responseBody, elapsed);
        }
        catch (Exception ex)
        {
            elapsed = Stopwatch.GetElapsedTime(start);
            await LogRequestAsync(context, requestId, requestBody, responseBody, elapsed, ex);
            throw;
        }
        finally
        {
            DebugContext.Current.Value = null;
        }
    }

    private bool ShouldSkipRequest(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant();
        
        // Skip debug dashboard requests
        if (path?.StartsWith("/_debug") == true)
            return true;

        // Skip excluded paths
        if (_config.ExcludedPaths.Any(excluded => path?.Contains(excluded.ToLowerInvariant()) == true))
            return true;

        // Skip static files
        if (path?.Contains('.') == true && 
            (path.EndsWith(".js") || path.EndsWith(".css") || path.EndsWith(".png") || 
             path.EndsWith(".jpg") || path.EndsWith(".jpeg") || path.EndsWith(".gif") || 
             path.EndsWith(".svg") || path.EndsWith(".ico") || path.EndsWith(".woff") || 
             path.EndsWith(".woff2") || path.EndsWith(".ttf") || path.EndsWith(".eot")))
            return true;

        return false;
    }

    private async Task<string> ReadRequestBodyAsync(HttpContext context)
    {
        try
        {
            if (context.Request.ContentLength > _config.MaxBodySize)
                return $"[Body too large: {context.Request.ContentLength} bytes]";

            context.Request.EnableBuffering();
            context.Request.Body.Position = 0;

            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            return body;
        }
        catch (Exception ex)
        {
            return $"[Error reading request body: {ex.Message}]";
        }
    }

    private async Task<string> ReadResponseBodyAsync(CaptureStream capture, Stream originalBodyStream)
    {
        var buffer = capture.Buffer;
        string text;
        try
        {
            text = buffer.Length > _config.MaxBodySize
                ? $"[Body too large: {buffer.Length} bytes]"
                : Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        }
        catch (Exception ex)
        {
            text = $"[Error reading response body: {ex.Message}]";
        }

        // The client always gets the full response, whatever happened to the capture.
        buffer.Position = 0;
        await buffer.CopyToAsync(originalBodyStream);

        return text;
    }

    // Buffers the response, except event streams: those have to reach the client as they're written.
    private sealed class CaptureStream : Stream
    {
        private readonly Stream _original;
        private readonly HttpResponse _response;
        private Stream? _target;

        public MemoryStream Buffer { get; } = new();

        public CaptureStream(Stream original, HttpResponse response)
        {
            _original = original;
            _response = response;
        }

        private Stream Target => _target ??=
            _response.ContentType?.StartsWith("text/event-stream", StringComparison.OrdinalIgnoreCase) == true
                ? _original
                : Buffer;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() => Target.Flush();
        public override Task FlushAsync(CancellationToken ct) => Target.FlushAsync(ct);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => Target.Write(buffer, offset, count);
        public override void Write(ReadOnlySpan<byte> buffer) => Target.Write(buffer);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) => Target.WriteAsync(buffer, offset, count, ct);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) => Target.WriteAsync(buffer, ct);
    }

    private async Task LogRequestAsync(HttpContext context, string requestId, string requestBody, string responseBody, TimeSpan elapsed, Exception? exception = null)
    {
        try
        {
            var headers = new Dictionary<string, string>();
            
            foreach (var header in context.Request.Headers)
            {
                if (!_config.ExcludedHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase) && !ReplayMarker.IsHeader(header.Key))
                {
                    headers[header.Key] = string.Join(", ", header.Value.ToArray());
                }
            }

            var request = context.Request;
            requestBody = Redactor.RedactBody(requestBody, _config.RedactedBodyFields);
            responseBody = Redactor.RedactBody(responseBody, _config.RedactedBodyFields);

            // next() threw before the response started, so StatusCode is still whatever
            // ASP.NET Core defaults it to (200) rather than the 500 the client will actually
            // get. If the response already started, whatever status was written stands.
            var statusCode = exception != null && !context.Response.HasStarted
                ? StatusCodes.Status500InternalServerError
                : context.Response.StatusCode;

            var requestEntry = new RequestEntry
            {
                Id = Guid.NewGuid().ToString(),
                RequestId = requestId,
                Method = request.Method,
                Path = request.Path,
                QueryString = request.QueryString.HasValue ? request.QueryString.Value! : "",
                Url = $"{request.Scheme}://{request.Host}{request.Path}{request.QueryString}",
                Protocol = request.Protocol,
                IsHttps = request.IsHttps,
                ReplayOf = ReplayMarker.Read(request),
                Headers = headers,
                RequestBody = requestBody,
                ResponseBody = responseBody,
                StatusCode = statusCode,
                ExecutionTimeMs = (long)elapsed.TotalMilliseconds,
                DurationMs = DebugContext.RoundMs(elapsed.TotalMilliseconds),
                Timestamp = DateTime.UtcNow,
                UserAgent = request.Headers.UserAgent.ToString(),
                IPAddress = GetClientIpAddress(context),
                ContentType = request.ContentType,
                ResponseContentType = context.Response.ContentType,
                RequestSize = request.ContentLength ?? 0,
                ResponseSize = context.Response.ContentLength ?? 0,
                Exception = exception?.ToString()
            };

            // Pull in whatever the interceptor and logger attached during the request
            var tracked = _debugContext.CompleteRequest(requestId);
            if (tracked != null)
            {
                requestEntry.SqlQueries = tracked.SqlQueries;
                requestEntry.Logs = tracked.Logs;
                requestEntry.Dumps = tracked.Dumps;
                requestEntry.OutgoingHttp = tracked.OutgoingHttp;
            }

            await _storage.StoreRequestAsync(requestEntry);

            if (_config.EmitActivities)
            {
                DebugTelemetry.RecordRequest(requestEntry);
            }

            // Also log as exception if there was an error
            if (exception != null)
            {
                var exceptionEntry = new ExceptionEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    RequestId = requestId,
                    Method = context.Request.Method,
                    Path = context.Request.Path + context.Request.QueryString,
                    Message = exception.Message,
                    ExceptionType = exception.GetType().Name,
                    StackTrace = exception.StackTrace,
                    Timestamp = DateTime.UtcNow,
                    IPAddress = GetClientIpAddress(context),
                    UserAgent = context.Request.Headers.UserAgent.ToString(),
                    RequestBody = requestBody,
                    Headers = headers
                };

                await _storage.StoreExceptionAsync(exceptionEntry);
            }
        }
        catch (Exception ex)
        {
            // Don't throw exceptions from logging - just ignore silently
            Console.WriteLine($"Error logging request: {ex.Message}");
        }
    }

    private static string GetClientIpAddress(HttpContext context)
    {
        return context.Request.Headers["X-Forwarded-For"].FirstOrDefault() ??
               context.Request.Headers["X-Real-IP"].FirstOrDefault() ??
               context.Connection.RemoteIpAddress?.ToString() ?? 
               "Unknown";
    }
}
