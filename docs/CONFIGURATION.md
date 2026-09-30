# Configuration Guide

This guide covers all configuration options available in AspNetDebugDashboard.

## Basic Configuration

### Using Code Configuration

```csharp
builder.Services.AddDebugDashboard(config =>
{
    config.IsEnabled = true;
    config.LogRequestBodies = true;
    config.LogResponseBodies = false;
    config.LogSqlQueries = true;
    config.LogExceptions = true;
    config.MaxEntries = 1000;
});
```

### Using appsettings.json

```json
{
  "DebugDashboard": {
    "IsEnabled": true,
    "DatabasePath": "debug-dashboard.db",
    "BasePath": "/_debug",
    "MaxEntries": 1000,
    "LogRequestBodies": true,
    "LogResponseBodies": false,
    "LogSqlQueries": true,
    "LogExceptions": true,
    "ExcludedPaths": ["/_debug", "/favicon.ico", "/robots.txt"],
    "ExcludedHeaders": ["Authorization", "Cookie", "Set-Cookie", "X-Api-Key", "X-Auth-Token", "Proxy-Authorization"],
    "MaxBodySize": 1048576,
    "RetentionPeriod": "7.00:00:00",
    "CleanupInterval": "01:00:00",
    "MaxDatabaseSize": 104857600,
    "EnableRealTimeUpdates": true,
    "CaptureOutgoingHttp": true,
    "CaptureOutgoingHttpBodies": false,
    "MaxOutgoingBodySize": 65536,
    "CaptureUnattachedOutgoingHttp": false,
    "DumpMaxDepth": 6,
    "DumpMaxItems": 100,
    "DumpMaxSize": 65536,
    "MaxDumpsPerRequest": 50,
    "EnablePerformanceCounters": true,
    "EnableDetailedSqlLogging": true,
    "AllowDataExport": true,
    "AllowDataImport": false,
    "SlowQueryThresholdMs": 1000,
    "SlowRequestThresholdMs": 5000,
    "TimeZone": "UTC",
    "EnableStackTraceCapture": true,
    "MaxStackTraceDepth": 50,
    "EnableMemoryProfiling": false,
    "EnableCpuProfiling": false
  }
}
```

Then bind the configuration:

```csharp
builder.Services.Configure<DebugConfiguration>(builder.Configuration.GetSection("DebugDashboard"));
builder.Services.AddDebugDashboard();
```

## Configuration Options

### Core Settings

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `IsEnabled` | `bool` | `true` | Enable/disable the debug dashboard |
| `DatabasePath` | `string` | `"debug-dashboard.db"` | Path to LiteDB database file |
| `BasePath` | `string` | `"/_debug"` | Base URL path for the dashboard |
| `MaxEntries` | `int` | `1000` | Maximum entries kept per entry type (requests, queries, logs, exceptions, outgoing calls, dumps each count separately) |
| `EnableRealTimeUpdates` | `bool` | `true` | Serve live updates on `/api/stream`. When false the endpoint returns 404 and the UI polls every 5 seconds |

### Logging Settings

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `LogRequestBodies` | `bool` | `true` | Capture HTTP request bodies |
| `LogResponseBodies` | `bool` | `false` | Capture HTTP response bodies |
| `LogSqlQueries` | `bool` | `true` | Log Entity Framework queries |
| `LogExceptions` | `bool` | `true` | Log unhandled exceptions |
| `EnableDetailedSqlLogging` | `bool` | `true` | Include SQL parameter values in captured queries |
| `EnableStackTraceCapture` | `bool` | `true` | Capture stack traces for exceptions |
| `MaxStackTraceDepth` | `int` | `50` | Maximum stack trace depth |

### Performance Settings

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `MaxBodySize` | `int` | `1048576` | Maximum body size to capture (1MB) |
| `SlowQueryThresholdMs` | `int` | `1000` | Threshold for marking queries as slow |
| `SlowRequestThresholdMs` | `int` | `5000` | Threshold for marking requests as slow |
| `EnablePerformanceCounters` | `bool` | `true` | Enable performance monitoring |
| `EnableMemoryProfiling` | `bool` | `false` | Enable memory usage tracking |
| `EnableCpuProfiling` | `bool` | `false` | Enable CPU usage tracking |

### Security Settings

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `ExcludedPaths` | `List<string>` | `["/_debug", "/favicon.ico", "/robots.txt"]` | Paths to exclude from logging |
| `ExcludedHeaders` | `List<string>` | `["Authorization", "Cookie", "Set-Cookie", "X-Api-Key", "X-Auth-Token", "Proxy-Authorization"]` | Headers to exclude from logging |
| `RedactedBodyFields` | `List<string>` | `["password", "secret", "token", "apikey"]` | JSON and form field names whose values are stored as `***` (case-insensitive, substring match) |
| `AllowedEnvironments` | `List<string>` | `["Development"]` | Environments where the UI, API and EF capture run. `"*"` allows all |
| `AuthorizationFilter` | `Func<HttpContext, bool>?` | `null` | Checked on every UI and API request; returning false gives a 401. Code only, not bindable from appsettings |
| `AllowDataExport` | `bool` | `true` | Allow exporting debug data |
| `AllowDataImport` | `bool` | `false` | Allow importing debug data |

### Data Management Settings

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `RetentionPeriod` | `TimeSpan` | `7 days` | Entries older than this are deleted on each cleanup run |
| `CleanupInterval` | `TimeSpan?` | `1 hour` | How often cleanup runs. `null` means 1 hour |
| `MaxDatabaseSize` | `long` | `104857600` (100MB) | Size cap for the database file plus its `-log.db` file. Also the health check's warning threshold |
| `TimeZone` | `string` | `"UTC"` | Time zone for displaying timestamps |

### How cleanup works

A background service runs every `CleanupInterval`, only in environments where the dashboard is active. Each run:

1. Deletes entries older than `RetentionPeriod`.
2. Trims each entry type to its newest `MaxEntries`.
3. Checkpoints the LiteDB log and measures the database file plus `-log.db`.
4. If that's over `MaxDatabaseSize`, deletes the oldest 25% of every entry type and compacts the file. It repeats up to four times, then logs a warning if the file is still too big.

Compaction rebuilds the file in place, so writes pause briefly while it runs. Files are never deleted, and `:memory:` databases skip compaction. Errors in a run are logged and the next run goes ahead as normal. `POST /_debug/api/cleanup` trims to `MaxEntries` on demand.

### Outgoing HTTP Settings

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `CaptureOutgoingHttp` | `bool` | `true` | Record calls made through `HttpClient` (any instance, not only `IHttpClientFactory`) |
| `CaptureOutgoingHttpBodies` | `bool` | `false` | Also store request and response bodies. Request bodies are read only when already buffered (`StringContent`, `JsonContent`, form content); streamed bodies are skipped |
| `MaxOutgoingBodySize` | `int` | `65536` (64KB) | Bodies are cut off past this size |
| `CaptureUnattachedOutgoingHttp` | `bool` | `false` | Record calls made outside a request (background services, startup). Off by default, which also keeps an OTLP exporter's own traffic out of the list |

`ExcludedHeaders` applies to outgoing headers. URL userinfo is always stripped, and query parameters whose names match `RedactedBodyFields` are stored as `***`, same as bodies.

### Dump Settings

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `DumpMaxDepth` | `int` | `6` | Nesting depth before a branch is replaced with a placeholder |
| `DumpMaxItems` | `int` | `100` | Items (or properties) per level before the rest are dropped |
| `DumpMaxSize` | `int` | `65536` (64KB) | Maximum size of the stored JSON |
| `MaxDumpsPerRequest` | `int` | `50` | Dumps attached to one request's detail. Extra dumps are still stored and show in the Dumps tab |

A dump that hits any limit is flagged as truncated in the UI.

### Telemetry Settings

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `EmitActivities` | `bool` | `true` | Emit captured requests and queries as OpenTelemetry spans on the `AspNetDebugDashboard` source. Does nothing until you add that source to a tracer. See [OPENTELEMETRY.md](OPENTELEMETRY.md). |

## Environment-Specific Configuration

### Development Environment

```json
{
  "DebugDashboard": {
    "IsEnabled": true,
    "LogRequestBodies": true,
    "LogResponseBodies": true,
    "LogSqlQueries": true,
    "MaxEntries": 2000,
    "EnableDetailedSqlLogging": true,
    "EnableMemoryProfiling": true
  }
}
```

### Staging Environment

```json
{
  "DebugDashboard": {
    "IsEnabled": true,
    "LogRequestBodies": false,
    "LogResponseBodies": false,
    "LogSqlQueries": true,
    "MaxEntries": 500,
    "EnableDetailedSqlLogging": false,
    "ExcludedPaths": ["/_debug", "/favicon.ico", "/robots.txt", "/health", "/metrics"]
  }
}
```

### Production Environment

```json
{
  "DebugDashboard": {
    "IsEnabled": false
  }
}
```

## Security Considerations

### Sensitive Data Protection

```csharp
builder.Services.AddDebugDashboard(config =>
{
    // Exclude sensitive headers
    config.ExcludedHeaders = new List<string>
    {
        "Authorization",
        "Cookie",
        "X-API-Key",
        "X-Auth-Token",
        "X-User-Token",
        "Authentication",
        "Bearer"
    };
    
    // Exclude sensitive paths
    config.ExcludedPaths = new List<string>
    {
        "/_debug",
        "/admin",
        "/api/auth",
        "/api/payments",
        "/api/users/password",
        "/api/sensitive-data"
    };
    
    // Disable body logging for sensitive endpoints
    config.LogRequestBodies = false;
    config.LogResponseBodies = false;
});
```

## Performance Optimization

### High-Traffic Applications

```csharp
builder.Services.AddDebugDashboard(config =>
{
    // Reduce overhead for high-traffic apps
    config.MaxEntries = 500;
    config.LogRequestBodies = false;
    config.LogResponseBodies = false;
    config.MaxBodySize = 10 * 1024; // 10KB
    config.EnableDetailedSqlLogging = false;
    config.EnableMemoryProfiling = false;
    config.EnableCpuProfiling = false;
    
    // Exclude frequently called endpoints
    config.ExcludedPaths.AddRange(new[]
    {
        "/api/health",
        "/api/metrics",
        "/api/ping",
        "/favicon.ico",
        "/robots.txt"
    });
});
```

### Memory-Constrained Environments

```csharp
builder.Services.AddDebugDashboard(config =>
{
    // Minimize memory usage
    config.MaxEntries = 100;
    config.LogRequestBodies = false;
    config.LogResponseBodies = false;
    config.MaxBodySize = 1024; // 1KB
    config.RetentionPeriod = TimeSpan.FromHours(1);
    config.EnablePerformanceCounters = false;
    config.EnableDetailedSqlLogging = false;
});
```

## Custom Database Path

### Temporary Directory

```csharp
builder.Services.AddDebugDashboard(config =>
{
    config.DatabasePath = Path.Combine(Path.GetTempPath(), "debug-dashboard.db");
});
```

### User-Specific Directory

```csharp
builder.Services.AddDebugDashboard(config =>
{
    var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    config.DatabasePath = Path.Combine(userProfile, ".debug-dashboard", "dashboard.db");
});
```

### In-Memory Database (Testing)

```csharp
builder.Services.AddDebugDashboard(config =>
{
    config.DatabasePath = ":memory:"; // LiteDB in-memory database
});
```

## Conditional Configuration

### Based on Environment

```csharp
builder.Services.AddDebugDashboard(config =>
{
    if (builder.Environment.IsDevelopment())
    {
        config.LogRequestBodies = true;
        config.LogResponseBodies = true;
        config.EnableDetailedSqlLogging = true;
        config.MaxEntries = 2000;
    }
    else if (builder.Environment.IsStaging())
    {
        config.LogRequestBodies = false;
        config.LogResponseBodies = false;
        config.EnableDetailedSqlLogging = false;
        config.MaxEntries = 500;
    }
    else // Production
    {
        config.IsEnabled = false;
    }
});
```

### Based on Configuration

```csharp
var enableDashboard = builder.Configuration.GetValue<bool>("Features:EnableDebugDashboard");

if (enableDashboard)
{
    builder.Services.AddDebugDashboard(config =>
    {
        // Configure based on other settings
        var logLevel = builder.Configuration.GetValue<string>("Logging:LogLevel:Default");
        config.EnableDetailedSqlLogging = logLevel == "Debug";
    });
}
```

## Checking the active configuration

`GET /_debug/api/config` returns the configuration the dashboard is actually running with. Useful when appsettings binding and code configuration disagree.

If something doesn't behave the way the options suggest, see [TROUBLESHOOTING.md](TROUBLESHOOTING.md).
