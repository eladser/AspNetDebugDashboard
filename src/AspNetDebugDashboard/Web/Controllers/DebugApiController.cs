using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AspNetDebugDashboard.Web.Controllers;

[ApiController]
[Route("/_debug/api")]
public class DebugApiController : ControllerBase
{
    private readonly IDebugStorage _storage;
    private readonly DebugConfiguration _config;

    public DebugApiController(IDebugStorage storage, IOptions<DebugConfiguration> config)
    {
        _storage = storage;
        _config = config.Value;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<DebugStats>> GetStats()
    {
        if (!_config.IsEnabled) return NotFound();
        
        var stats = await _storage.GetStatsAsync();
        return Ok(stats);
    }

    [HttpGet("requests")]
    public async Task<ActionResult<PagedResult<RequestEntry>>> GetRequests([FromQuery] DebugFilter filter)
    {
        if (!_config.IsEnabled) return NotFound();
        
        var requests = await _storage.GetRequestsAsync(filter);
        return Ok(requests);
    }

    [HttpGet("requests/{id}")]
    public async Task<ActionResult<RequestEntry>> GetRequest(string id)
    {
        if (!_config.IsEnabled) return NotFound();
        
        var request = await _storage.GetRequestAsync(id);
        if (request == null) return NotFound();
        
        return Ok(request);
    }

    [HttpGet("queries")]
    public async Task<ActionResult<PagedResult<SqlQueryEntry>>> GetQueries([FromQuery] DebugFilter filter)
    {
        if (!_config.IsEnabled) return NotFound();
        
        var queries = await _storage.GetSqlQueriesAsync(filter);
        return Ok(queries);
    }

    [HttpGet("queries/{id}")]
    public async Task<ActionResult<SqlQueryEntry>> GetQuery(string id)
    {
        if (!_config.IsEnabled) return NotFound();
        
        var query = await _storage.GetSqlQueryAsync(id);
        if (query == null) return NotFound();
        
        return Ok(query);
    }

    [HttpGet("logs")]
    public async Task<ActionResult<PagedResult<LogEntry>>> GetLogs([FromQuery] DebugFilter filter)
    {
        if (!_config.IsEnabled) return NotFound();
        
        var logs = await _storage.GetLogsAsync(filter);
        return Ok(logs);
    }

    [HttpGet("logs/{id}")]
    public async Task<ActionResult<LogEntry>> GetLog(string id)
    {
        if (!_config.IsEnabled) return NotFound();
        
        var log = await _storage.GetLogAsync(id);
        if (log == null) return NotFound();
        
        return Ok(log);
    }

    [HttpGet("exceptions")]
    public async Task<ActionResult<PagedResult<ExceptionEntry>>> GetExceptions([FromQuery] DebugFilter filter)
    {
        if (!_config.IsEnabled) return NotFound();
        
        var exceptions = await _storage.GetExceptionsAsync(filter);
        return Ok(exceptions);
    }

    [HttpGet("exceptions/{id}")]
    public async Task<ActionResult<ExceptionEntry>> GetException(string id)
    {
        if (!_config.IsEnabled) return NotFound();
        
        var exception = await _storage.GetExceptionAsync(id);
        if (exception == null) return NotFound();
        
        return Ok(exception);
    }

    [HttpPost("logs")]
    public async Task<ActionResult<string>> CreateLog([FromBody] CreateLogRequest request)
    {
        if (!_config.IsEnabled) return NotFound();
        
        var logEntry = new LogEntry
        {
            Message = request.Message,
            Level = request.Level ?? "Info",
            Tag = request.Tag,
            Category = request.Category,
            Properties = request.Properties ?? new Dictionary<string, object>(),
            RequestId = HttpContext.TraceIdentifier
        };
        
        var id = await _storage.StoreLogAsync(logEntry);
        return Ok(new { id });
    }

    [HttpDelete("clear")]
    public async Task<ActionResult> ClearAll()
    {
        if (!_config.IsEnabled) return NotFound();
        
        await _storage.ClearAllAsync();
        return Ok(new { message = "All debug data cleared" });
    }

    [HttpPost("cleanup")]
    public async Task<ActionResult> Cleanup()
    {
        if (!_config.IsEnabled) return NotFound();
        
        await _storage.CleanupAsync(_config.MaxEntries);
        return Ok(new { message = "Cleanup completed" });
    }

    [HttpGet("config")]
    public ActionResult<object> GetConfig()
    {
        if (!_config.IsEnabled) return NotFound();
        
        return Ok(new
        {
            isEnabled = _config.IsEnabled,
            maxEntries = _config.MaxEntries,
            logRequestBodies = _config.LogRequestBodies,
            logResponseBodies = _config.LogResponseBodies,
            logSqlQueries = _config.LogSqlQueries,
            logExceptions = _config.LogExceptions,
            enableRealTimeUpdates = _config.EnableRealTimeUpdates
        });
    }
}

public class CreateLogRequest
{
    public string Message { get; set; } = string.Empty;
    public string? Level { get; set; }
    public string? Tag { get; set; }
    public string? Category { get; set; }
    public Dictionary<string, object>? Properties { get; set; }
}
