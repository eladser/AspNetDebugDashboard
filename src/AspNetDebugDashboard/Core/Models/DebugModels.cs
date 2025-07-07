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
