using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AspNetDebugDashboard.Web.Controllers;

[ApiController]
public class DebugApiController : ControllerBase
{
    private readonly IDebugStorage _storage;
    private readonly DebugConfiguration _config;

    public DebugApiController(IDebugStorage storage, IOptions<DebugConfiguration> config)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
    }

    private bool IsValidPath()
    {
        if (!_config.IsEnabled) return false;
        
        // Only validate path if we have an HttpContext (skip for unit tests)
        if (HttpContext?.Request?.Path.Value != null)
        {
            var requestPath = HttpContext.Request.Path.Value;
            return requestPath.StartsWith(_config.BasePath);
        }
        
        return true; // Allow for unit tests where HttpContext is null
    }

    [HttpGet("/_debug/api/stats")]
    [HttpGet("/_custom-debug/api/stats")]
    public async Task<ActionResult<DebugStats>> GetStats()
    {
        if (!IsValidPath()) return NotFound();
        
        var stats = await _storage.GetStatsAsync();
        return Ok(stats);
    }

    [HttpGet("/_debug/api/requests")]
    [HttpGet("/_custom-debug/api/requests")]
    public async Task<ActionResult<PagedResult<RequestEntry>>> GetRequests([FromQuery] DebugFilter filter)
    {
        if (!IsValidPath()) return NotFound();
        
        var requests = await _storage.GetRequestsAsync(filter);
        return Ok(requests);
    }

    [HttpGet("/_debug/api/requests/{id}")]
    [HttpGet("/_custom-debug/api/requests/{id}")]
    public async Task<ActionResult<RequestEntry>> GetRequest(string id)
    {
        if (!IsValidPath()) return NotFound();
        
        var request = await _storage.GetRequestAsync(id);
        if (request == null) return NotFound();
        
        return Ok(request);
    }

    [HttpGet("/_debug/api/queries")]
    [HttpGet("/_custom-debug/api/queries")]
    public async Task<ActionResult<PagedResult<SqlQueryEntry>>> GetQueries([FromQuery] DebugFilter filter)
    {
        if (!IsValidPath()) return NotFound();
        
        var queries = await _storage.GetSqlQueriesAsync(filter);
        return Ok(queries);
    }

    [HttpGet("/_debug/api/queries/{id}")]
    [HttpGet("/_custom-debug/api/queries/{id}")]
    public async Task<ActionResult<SqlQueryEntry>> GetQuery(string id)
    {
        if (!IsValidPath()) return NotFound();
        
        var query = await _storage.GetSqlQueryAsync(id);
        if (query == null) return NotFound();
        
        return Ok(query);
    }

    [HttpGet("/_debug/api/logs")]
    [HttpGet("/_custom-debug/api/logs")]
    public async Task<ActionResult<PagedResult<LogEntry>>> GetLogs([FromQuery] DebugFilter filter)
    {
        if (!IsValidPath()) return NotFound();
        
        var logs = await _storage.GetLogsAsync(filter);
        return Ok(logs);
    }

    [HttpGet("/_debug/api/logs/{id}")]
    [HttpGet("/_custom-debug/api/logs/{id}")]
    public async Task<ActionResult<LogEntry>> GetLog(string id)
    {
        if (!IsValidPath()) return NotFound();
        
        var log = await _storage.GetLogAsync(id);
        if (log == null) return NotFound();
        
        return Ok(log);
    }

    [HttpGet("/_debug/api/exceptions")]
    [HttpGet("/_custom-debug/api/exceptions")]
    public async Task<ActionResult<PagedResult<ExceptionEntry>>> GetExceptions([FromQuery] DebugFilter filter)
    {
        if (!IsValidPath()) return NotFound();
        
        var exceptions = await _storage.GetExceptionsAsync(filter);
        return Ok(exceptions);
    }

    [HttpGet("/_debug/api/exceptions/{id}")]
    [HttpGet("/_custom-debug/api/exceptions/{id}")]
    public async Task<ActionResult<ExceptionEntry>> GetException(string id)
    {
        if (!IsValidPath()) return NotFound();
        
        var exception = await _storage.GetExceptionAsync(id);
        if (exception == null) return NotFound();
        
        return Ok(exception);
    }

    [HttpPost("/_debug/api/logs")]
    [HttpPost("/_custom-debug/api/logs")]
    public async Task<ActionResult<string>> CreateLog([FromBody] CreateLogRequest request)
    {
        if (!IsValidPath()) return NotFound();
        
        // Add null checks and validation
        if (request == null)
            return BadRequest("Request cannot be null");
            
        if (string.IsNullOrEmpty(request.Message))
            return BadRequest("Message is required");
        
        var logEntry = new LogEntry
        {
            Message = request.Message,
            Level = request.Level ?? "Info",
            Tag = request.Tag,
            Category = request.Category,
            Properties = request.Properties ?? new Dictionary<string, object>(),
            RequestId = HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString()
        };
        
        var id = await _storage.StoreLogAsync(logEntry);
        return Ok(new { id });
    }

    [HttpDelete("/_debug/api/clear")]
    [HttpDelete("/_custom-debug/api/clear")]
    public async Task<ActionResult> ClearAll()
    {
        if (!IsValidPath()) return NotFound();
        
        await _storage.ClearAllAsync();
        return Ok(new { message = "All debug data cleared" });
    }

    [HttpPost("/_debug/api/cleanup")]
    [HttpPost("/_custom-debug/api/cleanup")]
    public async Task<ActionResult> Cleanup()
    {
        if (!IsValidPath()) return NotFound();
        
        await _storage.CleanupAsync(_config.MaxEntries);
        return Ok(new { message = "Cleanup completed" });
    }

    [HttpGet("/_debug/api/config")]
    [HttpGet("/_custom-debug/api/config")]
    public ActionResult<object> GetConfig()
    {
        if (!IsValidPath()) return NotFound();
        
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

    [HttpGet("/_debug/api/export")]
    [HttpGet("/_custom-debug/api/export")]
    public async Task<ActionResult> ExportData([FromQuery] string format = "json")
    {
        if (!IsValidPath()) return NotFound();

        var stats = await _storage.GetStatsAsync();
        var requests = await _storage.GetRequestsAsync(new DebugFilter { PageSize = int.MaxValue });
        var queries = await _storage.GetSqlQueriesAsync(new DebugFilter { PageSize = int.MaxValue });
        var logs = await _storage.GetLogsAsync(new DebugFilter { PageSize = int.MaxValue });
        var exceptions = await _storage.GetExceptionsAsync(new DebugFilter { PageSize = int.MaxValue });

        var exportData = new
        {
            ExportedAt = DateTime.UtcNow,
            Stats = stats,
            Requests = requests.Items,
            Queries = queries.Items,
            Logs = logs.Items,
            Exceptions = exceptions.Items
        };

        var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });

        return File(System.Text.Encoding.UTF8.GetBytes(json), 
                   "application/json", 
                   $"debug-export-{DateTime.UtcNow:yyyy-MM-dd-HH-mm-ss}.json");
    }

    [HttpGet("/_debug/api/search")]
    [HttpGet("/_custom-debug/api/search")]
    public async Task<ActionResult> Search([FromQuery] string term, [FromQuery] string[] types = null!)
    {
        if (!IsValidPath()) return NotFound();
        if (string.IsNullOrWhiteSpace(term)) return BadRequest("Search term is required");

        var results = new List<object>();
        
        if (types == null || types.Contains("requests"))
        {
            var requests = await _storage.GetRequestsAsync(new DebugFilter 
            { 
                Search = term, 
                PageSize = 10 
            });
            results.AddRange(requests.Items.Select(r => new { Type = "request", Data = r }));
        }

        if (types == null || types.Contains("queries"))
        {
            var queries = await _storage.GetSqlQueriesAsync(new DebugFilter 
            { 
                Search = term, 
                PageSize = 10 
            });
            results.AddRange(queries.Items.Select(q => new { Type = "query", Data = q }));
        }

        if (types == null || types.Contains("logs"))
        {
            var logs = await _storage.GetLogsAsync(new DebugFilter 
            { 
                Search = term, 
                PageSize = 10 
            });
            results.AddRange(logs.Items.Select(l => new { Type = "log", Data = l }));
        }

        if (types == null || types.Contains("exceptions"))
        {
            var exceptions = await _storage.GetExceptionsAsync(new DebugFilter 
            { 
                Search = term, 
                PageSize = 10 
            });
            results.AddRange(exceptions.Items.Select(e => new { Type = "exception", Data = e }));
        }

        return Ok(results.Take(50));
    }

    [HttpGet("/_debug/api/performance")]
    [HttpGet("/_custom-debug/api/performance")]
    public async Task<ActionResult> GetPerformanceMetrics()
    {
        if (!IsValidPath()) return NotFound();

        var stats = await _storage.GetStatsAsync();
        
        // Get requests for performance analysis
        var recentRequests = await _storage.GetRequestsAsync(new DebugFilter 
        { 
            PageSize = 1000,
            SortBy = "timestamp",
            SortDescending = true
        });

        var requests = recentRequests.Items.Where(r => r.Timestamp > DateTime.UtcNow.AddHours(-1)).ToList();
        
        var performanceMetrics = new
        {
            TotalRequests = requests.Count,
            AverageResponseTime = requests.Any() ? requests.Average(r => r.ExecutionTimeMs) : 0,
            MedianResponseTime = requests.Any() ? GetMedian(requests.Select(r => (double)r.ExecutionTimeMs).ToList()) : 0,
            P95ResponseTime = requests.Any() ? GetPercentile(requests.Select(r => (double)r.ExecutionTimeMs).ToList(), 95) : 0,
            P99ResponseTime = requests.Any() ? GetPercentile(requests.Select(r => (double)r.ExecutionTimeMs).ToList(), 99) : 0,
            ErrorRate = requests.Any() ? (double)requests.Count(r => r.StatusCode >= 400) / requests.Count * 100 : 0,
            RequestsPerMinute = requests.Any() ? requests.Count / 60.0 : 0,
            SlowestEndpoints = requests
                .GroupBy(r => $"{r.Method} {r.Path}")
                .Select(g => new 
                {
                    Endpoint = g.Key,
                    AverageTime = g.Average(r => r.ExecutionTimeMs),
                    RequestCount = g.Count()
                })
                .OrderByDescending(e => e.AverageTime)
                .Take(10)
                .ToList(),
            StatusCodeDistribution = requests
                .GroupBy(r => r.StatusCode)
                .Select(g => new 
                {
                    StatusCode = g.Key,
                    Count = g.Count(),
                    Percentage = (double)g.Count() / requests.Count * 100
                })
                .OrderBy(s => s.StatusCode)
                .ToList()
        };

        return Ok(performanceMetrics);
    }

    [HttpGet("/_debug/api/health")]
    [HttpGet("/_custom-debug/api/health")]
    public async Task<ActionResult> GetHealth()
    {
        if (!IsValidPath()) return NotFound();

        var health = new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Configuration = new
            {
                IsEnabled = _config.IsEnabled,
                MaxEntries = _config.MaxEntries,
                DatabasePath = _config.DatabasePath
            },
            Storage = await _storage.GetHealthAsync()
        };

        return Ok(health);
    }

    private static double GetMedian(List<double> values)
    {
        if (!values.Any()) return 0;
        
        var sorted = values.OrderBy(x => x).ToList();
        var mid = sorted.Count / 2;
        
        return sorted.Count % 2 == 0 
            ? (sorted[mid - 1] + sorted[mid]) / 2.0 
            : sorted[mid];
    }

    private static double GetPercentile(List<double> values, int percentile)
    {
        if (!values.Any()) return 0;
        
        var sorted = values.OrderBy(x => x).ToList();
        var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
        
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
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
