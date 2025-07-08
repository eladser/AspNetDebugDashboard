using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AspNetDebugDashboard.Middleware;

public class DebugExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DebugExceptionMiddleware> _logger;
    private readonly IDebugStorage _storage;
    private readonly DebugContext _context;
    private readonly DebugConfiguration _config;

    public DebugExceptionMiddleware(
        RequestDelegate next,
        ILogger<DebugExceptionMiddleware> logger,
        IDebugStorage storage,
        DebugContext context,
        IOptions<DebugConfiguration> config)
    {
        _next = next;
        _logger = logger;
        _storage = storage;
        _context = context;
        _config = config.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            if (_config.IsEnabled && _config.LogExceptions)
            {
                await LogExceptionAsync(context, ex);
            }
            
            _logger.LogError(ex, "An unhandled exception occurred");
            throw;
        }
    }

    private async Task LogExceptionAsync(HttpContext httpContext, Exception exception)
    {
        var requestId = httpContext.TraceIdentifier;
        
        var exceptionEntry = new ExceptionEntry
        {
            Type = "Exception",
            Message = exception.Message,
            StackTrace = exception.StackTrace,
            Source = exception.Source,
            RequestId = requestId,
            Route = httpContext.Request.Path,
            Method = httpContext.Request.Method,
            Path = httpContext.Request.Path,
            ExceptionType = exception.GetType().Name,
            Data = GetExceptionData(exception),
            InnerException = GetInnerException(exception.InnerException)
        };

        // Add to request context if exists
        _context.SetException(requestId, exceptionEntry);
        
        // Store separately for exception tracking
        await _storage.StoreExceptionAsync(exceptionEntry);
    }

    private Dictionary<string, object> GetExceptionData(Exception exception)
    {
        var data = new Dictionary<string, object>();
        
        foreach (var key in exception.Data.Keys)
        {
            try
            {
                var value = exception.Data[key];
                if (value != null)
                {
                    data[key.ToString() ?? "unknown"] = JsonSerializer.Serialize(value);
                }
            }
            catch
            {
                data[key.ToString() ?? "unknown"] = "[Serialization Error]";
            }
        }
        
        return data;
    }

    private ExceptionEntry? GetInnerException(Exception? innerException)
    {
        if (innerException == null) return null;
        
        return new ExceptionEntry
        {
            Type = "InnerException",
            Message = innerException.Message,
            StackTrace = innerException.StackTrace,
            Source = innerException.Source,
            ExceptionType = innerException.GetType().Name,
            Data = GetExceptionData(innerException),
            InnerException = GetInnerException(innerException.InnerException)
        };
    }
}
