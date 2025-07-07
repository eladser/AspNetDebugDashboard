using System.ComponentModel.DataAnnotations;

namespace AspNetDebugDashboard.Core.Models;

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

public class RequestEntry : DebugEntry
{
    public string Method { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string QueryString { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public long ExecutionTimeMs { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public string? RequestBody { get; set; }
    public string? ResponseBody { get; set; }
    public string? ContentType { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public List<SqlQueryEntry> SqlQueries { get; set; } = new();
    public List<LogEntry> Logs { get; set; } = new();
    public ExceptionEntry? Exception { get; set; }
    public long RequestSize { get; set; }
    public long ResponseSize { get; set; }
    public string? Protocol { get; set; }
    public bool IsHttps { get; set; }
}

public class SqlQueryEntry : DebugEntry
{
    public string Query { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public long ExecutionTimeMs { get; set; }
    public int RowsAffected { get; set; }
    public string? ConnectionString { get; set; }
    public string? Database { get; set; }
    public bool IsSuccessful { get; set; } = true;
    public string? Error { get; set; }
    public string? CommandType { get; set; }
    public bool IsSlowQuery { get; set; }
    public string? StackTrace { get; set; }
}

public class LogEntry : DebugEntry
{
    public string Level { get; set; } = "Info";
    public string Message { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Tag { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
    public string? StackTrace { get; set; }
    public string? Exception { get; set; }
    public int? ThreadId { get; set; }
    public string? MachineName { get; set; }
    public string? ProcessName { get; set; }
    public int? ProcessId { get; set; }
}

public class ExceptionEntry : DebugEntry
{
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? Source { get; set; }
    public string? Route { get; set; }
    public string? Method { get; set; }
    public string? Path { get; set; }
    public string? ExceptionType { get; set; }
    public ExceptionEntry? InnerException { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public string? RequestBody { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public string? TargetSite { get; set; }
    public int? HResult { get; set; }
    public string? HelpLink { get; set; }
}

public class DebugConfiguration
{
    public bool IsEnabled { get; set; } = true;
    public string DatabasePath { get; set; } = "debug-dashboard.db";
    public string BasePath { get; set; } = "/_debug";
    public int MaxEntries { get; set; } = 1000;
    public bool LogRequestBodies { get; set; } = true;
    public bool LogResponseBodies { get; set; } = false;
    public bool LogSqlQueries { get; set; } = true;
    public bool LogExceptions { get; set; } = true;
    public bool EnableRealTimeUpdates { get; set; } = true;
    public List<string> ExcludedPaths { get; set; } = new() { "/_debug", "/favicon.ico", "/robots.txt" };
    public List<string> ExcludedHeaders { get; set; } = new() { "Authorization", "Cookie" };
    public int MaxBodySize { get; set; } = 1024 * 1024; // 1MB
}
