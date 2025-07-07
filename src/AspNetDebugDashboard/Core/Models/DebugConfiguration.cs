namespace AspNetDebugDashboard.Core.Models;

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
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);
    public bool EnablePerformanceCounters { get; set; } = true;
    public bool EnableDetailedSqlLogging { get; set; } = true;
    public bool AllowDataExport { get; set; } = true;
    public bool AllowDataImport { get; set; } = false;
    public int SlowQueryThresholdMs { get; set; } = 1000;
    public int SlowRequestThresholdMs { get; set; } = 5000;
    public string TimeZone { get; set; } = "UTC";
    public bool EnableStackTraceCapture { get; set; } = true;
    public int MaxStackTraceDepth { get; set; } = 50;
    public bool EnableMemoryProfiling { get; set; } = false;
    public bool EnableCpuProfiling { get; set; } = false;
}
