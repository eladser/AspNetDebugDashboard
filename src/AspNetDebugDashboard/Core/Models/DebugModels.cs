using System.ComponentModel.DataAnnotations;

namespace AspNetDebugDashboard.Core.Models;

public class DebugFilter
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string SortBy { get; set; } = "timestamp";
    public bool SortDescending { get; set; } = true;
    
    // Filtering options
    public string? Search { get; set; }
    public string? StatusCode { get; set; }
    public string? Method { get; set; }
    public string? Level { get; set; }
    public string? ExceptionType { get; set; }
    public string? Path { get; set; }
    public string? Tag { get; set; }
    public string? Category { get; set; }
    
    // Date filtering
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    
    // Performance filtering
    public int? MinExecutionTime { get; set; }
    public int? MaxExecutionTime { get; set; }
    
    // User filtering
    public string? IPAddress { get; set; }
    public string? UserAgent { get; set; }
    
    // Request filtering
    public string? RequestId { get; set; }
    public bool? HasRequestBody { get; set; }
    public bool? HasResponseBody { get; set; }
    
    // SQL filtering
    public bool? IsSuccessful { get; set; }
    public bool? IsSlowQuery { get; set; }
    
    // Validation
    public bool IsValid()
    {
        return Page > 0 && 
               PageSize > 0 && 
               PageSize <= 1000 &&
               (DateFrom == null || DateTo == null || DateFrom <= DateTo);
    }
    
    public void Normalize()
    {
        Page = Math.Max(1, Page);
        PageSize = Math.Max(1, Math.Min(1000, PageSize));
        Search = Search?.Trim();
        SortBy = SortBy?.ToLowerInvariant() ?? "timestamp";
        
        // Ensure valid sort fields
        var validSortFields = new[] { "timestamp", "executiontimems", "statuscode", "method", "path", "level", "message" };
        if (!validSortFields.Contains(SortBy))
        {
            SortBy = "timestamp";
        }
    }
}

public class PagedResult<T>
{
    public IList<T> Items { get; set; } = new List<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

public class RequestEntry : DebugEntry
{
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = new();
    public string? RequestBody { get; set; }
    public string? ResponseBody { get; set; }
    public int StatusCode { get; set; }
    public double ExecutionTimeMs { get; set; }
    public string? UserAgent { get; set; }
    public string? IPAddress { get; set; }
    public string? ContentType { get; set; }
    public string? ResponseContentType { get; set; }
    public long RequestSize { get; set; }
    public long ResponseSize { get; set; }
    public string? Exception { get; set; }
    public Dictionary<string, object> QueryParameters { get; set; } = new();
    public string? Referrer { get; set; }
    public bool IsAjax { get; set; }
    public string? Protocol { get; set; }
    public bool IsHttps { get; set; }
}

public class SqlQueryEntry : DebugEntry
{
    public string Query { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public double ExecutionTimeMs { get; set; }
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public int? RowsAffected { get; set; }
    public string? CommandType { get; set; }
    public string? ConnectionString { get; set; }
    public string? DatabaseName { get; set; }
    public bool IsSlowQuery { get; set; }
    public string? StackTrace { get; set; }
    public string? QueryPlan { get; set; }
}

public class LogEntry : DebugEntry
{
    public string Message { get; set; } = string.Empty;
    public string Level { get; set; } = "Info";
    public string? Tag { get; set; }
    public string? Category { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
    public string? Exception { get; set; }
    public string? StackTrace { get; set; }
    public int? ThreadId { get; set; }
    public string? MachineName { get; set; }
    public string? ProcessName { get; set; }
    public int? ProcessId { get; set; }
}

public class ExceptionEntry : DebugEntry
{
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string ExceptionType { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? IPAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? RequestBody { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public string? InnerException { get; set; }
    public string? Source { get; set; }
    public string? TargetSite { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
    public int? HResult { get; set; }
    public string? HelpLink { get; set; }
}

public abstract class DebugEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string RequestId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? CorrelationId { get; set; }
    public string? UserId { get; set; }
    public string? SessionId { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class HealthCheckResult
{
    public string Status { get; set; } = "Unknown";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Details { get; set; } = new();
    public TimeSpan? ResponseTime { get; set; }
    public string? ErrorMessage { get; set; }
}

public class PerformanceMetrics
{
    public int TotalRequests { get; set; }
    public double AverageResponseTime { get; set; }
    public double MedianResponseTime { get; set; }
    public double P95ResponseTime { get; set; }
    public double P99ResponseTime { get; set; }
    public double ErrorRate { get; set; }
    public double RequestsPerMinute { get; set; }
    public List<EndpointPerformance> SlowestEndpoints { get; set; } = new();
    public List<StatusCodeDistribution> StatusCodeDistribution { get; set; } = new();
    public DateTime AnalyzedFrom { get; set; }
    public DateTime AnalyzedTo { get; set; }
}

public class EndpointPerformance
{
    public string Endpoint { get; set; } = string.Empty;
    public double AverageTime { get; set; }
    public int RequestCount { get; set; }
    public double ErrorRate { get; set; }
    public double P95Time { get; set; }
}

public class StatusCodeDistribution
{
    public int StatusCode { get; set; }
    public int Count { get; set; }
    public double Percentage { get; set; }
}

public class ExportData
{
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public string Version { get; set; } = "1.0.0";
    public DebugStats Stats { get; set; } = new();
    public List<RequestEntry> Requests { get; set; } = new();
    public List<SqlQueryEntry> Queries { get; set; } = new();
    public List<LogEntry> Logs { get; set; } = new();
    public List<ExceptionEntry> Exceptions { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class SearchResult
{
    public string Type { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public object Data { get; set; } = new();
    public double Relevance { get; set; }
}
