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
    [Obsolete("Has no effect. The dashboard polls the API.")]
    public bool EnableRealTimeUpdates { get; set; } = true;
    public List<string> ExcludedPaths { get; set; } = new() { "/_debug", "/favicon.ico", "/robots.txt" };
    public List<string> ExcludedHeaders { get; set; } = new()
    {
        "Authorization", "Cookie", "Set-Cookie", "X-Api-Key", "X-Auth-Token", "Proxy-Authorization"
    };

    // Body field names (case-insensitive substring match) redacted to "***" before storage.
    public List<string> RedactedBodyFields { get; set; } = new() { "password", "secret", "token", "apikey" };

    // Environments the dashboard responds in, checked against IHostEnvironment.EnvironmentName.
    // Use "*" to allow every environment. Everything (UI, API, EF interceptor) goes through this.
    public List<string> AllowedEnvironments { get; set; } = new() { "Development" };

    // Extra gate on top of IsEnabled/AllowedEnvironments, applied to every dashboard UI and API
    // request. Return false to reject with 401. Null (default) means no extra check.
    public Func<Microsoft.AspNetCore.Http.HttpContext, bool>? AuthorizationFilter { get; set; }

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
    
    // Missing properties referenced in the code
    public TimeSpan? CleanupInterval { get; set; } = TimeSpan.FromHours(1);
    public long MaxDatabaseSize { get; set; } = 100 * 1024 * 1024; // 100MB

    // Emit captured requests and queries as Activity spans on the
    // "AspNetDebugDashboard" ActivitySource. No effect unless something is
    // listening, so you add that source to your OpenTelemetry tracing to
    // forward them (Aspire, Jaeger, any OTLP backend).
    public bool EmitActivities { get; set; } = true;
}
