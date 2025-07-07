namespace AspNetDebugDashboard.Core.Models;

public class DebugStats
{
    public int TotalRequests { get; set; }
    public int TotalSqlQueries { get; set; }
    public int TotalExceptions { get; set; }
    public int TotalLogs { get; set; }
    public double AverageResponseTime { get; set; }
    public double AverageSqlTime { get; set; }
    public Dictionary<int, int> StatusCodeDistribution { get; set; } = new();
    public Dictionary<string, int> RequestMethodDistribution { get; set; } = new();
    public Dictionary<string, int> ExceptionTypeDistribution { get; set; } = new();
    public List<SlowRequest> SlowestRequests { get; set; } = new();
    public List<SlowQuery> SlowestQueries { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class SlowRequest
{
    public string Id { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long ExecutionTimeMs { get; set; }
    public DateTime Timestamp { get; set; }
}

public class SlowQuery
{
    public string Id { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public long ExecutionTimeMs { get; set; }
    public DateTime Timestamp { get; set; }
}

public class DebugFilter
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Method { get; set; }
    public string? Path { get; set; }
    public int? StatusCode { get; set; }
    public string? Level { get; set; }
    public string? Tag { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string SortBy { get; set; } = "timestamp";
    public bool SortDescending { get; set; } = true;
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
