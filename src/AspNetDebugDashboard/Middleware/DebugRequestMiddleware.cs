using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

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

        var stopwatch = Stopwatch.StartNew();
        var requestId = context.TraceIdentifier;
        var requestBody = "";
        var responseBody = "";

        // Register the request so the EF interceptor and IDebugLogger can
        // attach queries/logs to it while it's in flight.
        _debugContext.StartRequest(requestId, new RequestEntry { RequestId = requestId });

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
                using var responseBodyStream = new MemoryStream();
                context.Response.Body = responseBodyStream;

                await _next(context);

                stopwatch.Stop();
                responseBody = await ReadResponseBodyAsync(context, responseBodyStream, originalBodyStream);
            }
            else
            {
                await _next(context);
                stopwatch.Stop();
            }

            // Log the request
            await LogRequestAsync(context, requestId, requestBody, responseBody, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await LogRequestAsync(context, requestId, requestBody, responseBody, stopwatch.ElapsedMilliseconds, ex);
            throw;
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

    private async Task<string> ReadResponseBodyAsync(HttpContext context, MemoryStream responseBodyStream, Stream originalBodyStream)
    {
        try
        {
            if (responseBodyStream.Length > _config.MaxBodySize)
                return $"[Body too large: {responseBodyStream.Length} bytes]";

            responseBodyStream.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(responseBodyStream).ReadToEndAsync();
            
            responseBodyStream.Seek(0, SeekOrigin.Begin);
            await responseBodyStream.CopyToAsync(originalBodyStream);
            context.Response.Body = originalBodyStream;

            return responseBody;
        }
        catch (Exception ex)
        {
            return $"[Error reading response body: {ex.Message}]";
        }
    }

    private async Task LogRequestAsync(HttpContext context, string requestId, string requestBody, string responseBody, long elapsedMs, Exception? exception = null)
    {
        try
        {
            var headers = new Dictionary<string, string>();
            
            foreach (var header in context.Request.Headers)
            {
                if (!_config.ExcludedHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase))
                {
                    headers[header.Key] = string.Join(", ", header.Value.ToArray());
                }
            }

            var request = context.Request;
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
                Headers = headers,
                RequestBody = requestBody,
                ResponseBody = responseBody,
                StatusCode = context.Response.StatusCode,
                ExecutionTimeMs = elapsedMs,
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
            }

            await _storage.StoreRequestAsync(requestEntry);

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
