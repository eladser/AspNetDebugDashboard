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
    "EnableRealTimeUpdates": true,
    "ExcludedPaths": ["/_debug", "/favicon.ico", "/robots.txt"],
    "ExcludedHeaders": ["Authorization", "Cookie"],
    "MaxBodySize": 1048576,
    "RetentionPeriod": "7.00:00:00",
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
| `MaxEntries` | `int` | `1000` | Maximum number of entries to keep |

### Logging Settings

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `LogRequestBodies` | `bool` | `true` | Capture HTTP request bodies |
| `LogResponseBodies` | `bool` | `false` | Capture HTTP response bodies |
| `LogSqlQueries` | `bool` | `true` | Log Entity Framework queries |
| `LogExceptions` | `bool` | `true` | Log unhandled exceptions |
| `EnableDetailedSqlLogging` | `bool` | `true` | Include SQL parameters and execution plans |
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
| `ExcludedHeaders` | `List<string>` | `["Authorization", "Cookie"]` | Headers to exclude from logging |
| `AllowDataExport` | `bool` | `true` | Allow exporting debug data |
| `AllowDataImport` | `bool` | `false` | Allow importing debug data |

### Data Management Settings

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `RetentionPeriod` | `TimeSpan` | `7 days` | How long to keep debug data |
| `EnableRealTimeUpdates` | `bool` | `true` | Enable real-time dashboard updates |
| `TimeZone` | `string` | `"UTC"` | Time zone for displaying timestamps |

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

### Custom Security Rules

```csharp
public class SecurityAwareDebugConfiguration : DebugConfiguration
{
    public SecurityAwareDebugConfiguration()
    {
        // Apply security-first defaults
        LogRequestBodies = false;
        LogResponseBodies = false;
        AllowDataExport = false;
        AllowDataImport = false;
        
        // Enhanced exclusions
        ExcludedHeaders.AddRange(new[]
        {
            "X-Forwarded-For",
            "X-Real-IP",
            "X-User-ID",
            "X-Session-ID"
        });
        
        ExcludedPaths.AddRange(new[]
        {
            "/api/admin",
            "/api/internal",
            "/health",
            "/metrics"
        });
    }
}
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

## Validation and Testing

### Configuration Validation

```csharp
public static class DebugConfigurationValidator
{
    public static void Validate(DebugConfiguration config)
    {
        if (config.MaxEntries <= 0)
            throw new ArgumentException("MaxEntries must be greater than 0");
        
        if (config.MaxBodySize <= 0)
            throw new ArgumentException("MaxBodySize must be greater than 0");
        
        if (string.IsNullOrEmpty(config.DatabasePath))
            throw new ArgumentException("DatabasePath cannot be null or empty");
        
        if (config.SlowQueryThresholdMs <= 0)
            throw new ArgumentException("SlowQueryThresholdMs must be greater than 0");
    }
}
```

### Testing Configuration

```csharp
[Test]
public void Configuration_Should_Be_Valid()
{
    var config = new DebugConfiguration();
    
    // Test default values
    Assert.IsTrue(config.IsEnabled);
    Assert.AreEqual(1000, config.MaxEntries);
    Assert.AreEqual("/_debug", config.BasePath);
    
    // Test validation
    Assert.DoesNotThrow(() => DebugConfigurationValidator.Validate(config));
}
```

## Best Practices

1. **Security First**: Always review exclusion lists for sensitive data
2. **Performance**: Monitor the impact on application performance
3. **Storage**: Consider disk space usage for the database
4. **Environment**: Use different configurations per environment
5. **Retention**: Set appropriate retention periods
6. **Monitoring**: Monitor dashboard usage and performance impact

## Troubleshooting Configuration

### Common Issues

1. **Dashboard Not Loading**: Check `IsEnabled` and environment settings
2. **No SQL Queries**: Verify EF Core interceptor configuration
3. **High Memory Usage**: Reduce `MaxEntries` and disable body logging
4. **Performance Issues**: Increase exclusion lists and reduce logging detail

### Debug Configuration

```csharp
// Add this endpoint to debug configuration
app.MapGet("/debug-config", (IOptions<DebugConfiguration> options) => 
{
    return Results.Ok(options.Value);
});
```
