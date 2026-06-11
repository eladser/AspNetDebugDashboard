using AspNetDebugDashboard.Core.Models;
using Microsoft.AspNetCore.Http;

namespace AspNetDebugDashboard.Core.Services;

public interface IDebugLogger
{
    Task LogAsync(string message, string level = "Info", string? tag = null, Dictionary<string, object>? properties = null);
    Task LogInfoAsync(string message, string? tag = null, Dictionary<string, object>? properties = null);
    Task LogWarningAsync(string message, string? tag = null, Dictionary<string, object>? properties = null);
    Task LogErrorAsync(string message, string? tag = null, Dictionary<string, object>? properties = null);
    Task LogSuccessAsync(string message, string? tag = null, Dictionary<string, object>? properties = null);
}

public class DebugLogger : IDebugLogger
{
    private readonly IDebugStorage _storage;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly DebugContext _debugContext;

    public DebugLogger(IDebugStorage storage, IHttpContextAccessor httpContextAccessor, DebugContext debugContext)
    {
        _storage = storage;
        _httpContextAccessor = httpContextAccessor;
        _debugContext = debugContext;
    }

    public async Task LogAsync(string message, string level = "Info", string? tag = null, Dictionary<string, object>? properties = null)
    {
        var context = _httpContextAccessor.HttpContext;
        var requestId = context?.TraceIdentifier;

        var logEntry = new LogEntry
        {
            Message = message,
            Level = level,
            Tag = tag,
            RequestId = requestId ?? string.Empty,
            Properties = properties ?? new Dictionary<string, object>()
        };

        await _storage.StoreLogAsync(logEntry);

        // attach to the in-flight request so it shows up in the request detail
        if (!string.IsNullOrEmpty(requestId))
        {
            _debugContext.AddLog(requestId, logEntry);
        }
    }

    public async Task LogInfoAsync(string message, string? tag = null, Dictionary<string, object>? properties = null)
    {
        await LogAsync(message, "Info", tag, properties);
    }

    public async Task LogWarningAsync(string message, string? tag = null, Dictionary<string, object>? properties = null)
    {
        await LogAsync(message, "Warning", tag, properties);
    }

    public async Task LogErrorAsync(string message, string? tag = null, Dictionary<string, object>? properties = null)
    {
        await LogAsync(message, "Error", tag, properties);
    }

    public async Task LogSuccessAsync(string message, string? tag = null, Dictionary<string, object>? properties = null)
    {
        await LogAsync(message, "Success", tag, properties);
    }
}
