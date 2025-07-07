using AspNetDebugDashboard.Core.Models;

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

    public DebugLogger(IDebugStorage storage, IHttpContextAccessor httpContextAccessor)
    {
        _storage = storage;
        _httpContextAccessor = httpContextAccessor;
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
            RequestId = requestId,
            Properties = properties ?? new Dictionary<string, object>()
        };

        await _storage.StoreLogAsync(logEntry);
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
