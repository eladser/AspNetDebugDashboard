# Production Setup Guide

## 🚀 Quick Start

### 1. Install NuGet Package
```bash
dotnet add package AspNetDebugDashboard
```

### 2. Configure Services
```csharp
// Program.cs (ASP.NET Core 6+)
using AspNetDebugDashboard.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add Debug Dashboard
builder.Services.AddDebugDashboard(options =>
{
    options.IsEnabled = builder.Environment.IsDevelopment();
    options.LogRequestBodies = true;
    options.LogResponseBodies = true;
    options.LogSqlQueries = true;
    options.LogExceptions = true;
    options.MaxEntries = 10000;
    options.DatabasePath = "debug-dashboard.db";
});

// Add Entity Framework with Debug Dashboard
builder.Services.AddDbContext<YourDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    options.AddDebugDashboardInterceptor(); // Add this line
});

var app = builder.Build();

// Use Debug Dashboard (should be early in pipeline)
app.UseDebugDashboard();

// Your other middleware...
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

### 3. Access Dashboard
Navigate to `https://localhost:5001/_debug` to view the dashboard.

## 🔧 Configuration Options

### Basic Configuration
```csharp
builder.Services.AddDebugDashboard(options =>
{
    // Enable/disable dashboard
    options.IsEnabled = true;
    
    // Database settings
    options.DatabasePath = "debug-dashboard.db";
    options.MaxEntries = 10000;
    
    // Request/Response logging
    options.LogRequestBodies = true;
    options.LogResponseBodies = true;
    options.MaxBodySize = 1024 * 1024; // 1MB
    
    // SQL logging
    options.LogSqlQueries = true;
    options.EnableDetailedSqlLogging = true;
    options.SlowQueryThresholdMs = 1000;
    
    // Exception logging
    options.LogExceptions = true;
    options.EnableStackTraceCapture = true;
    options.MaxStackTraceDepth = 50;
    
    // Performance monitoring
    options.EnablePerformanceCounters = true;
    options.SlowRequestThresholdMs = 2000;
    
    // Data export/import
    options.AllowDataExport = true;
    options.AllowDataImport = false;
    
    // Cleanup settings
    options.RetentionPeriod = TimeSpan.FromDays(7);
    
    // Security settings
    options.ExcludedPaths = new[] { "/health", "/metrics" };
    options.ExcludedHeaders = new[] { "Authorization", "Cookie" };
});
```

### Advanced Configuration
```csharp
// Custom storage configuration
builder.Services.AddDebugDashboard(options =>
{
    options.DatabasePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YourApp",
        "debug-dashboard.db"
    );
    
    // Custom time zone
    options.TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
    
    // Memory and CPU profiling
    options.EnableMemoryProfiling = true;
    options.EnableCpuProfiling = true;
    
    // Real-time updates
    options.EnableRealTimeUpdates = true;
});
```

## 🎯 Entity Framework Integration

### 1. Add Interceptor to DbContext
```csharp
public class YourDbContext : DbContext
{
    public YourDbContext(DbContextOptions<YourDbContext> options) : base(options) { }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddDebugDashboardInterceptor();
    }
}
```

### 2. Alternative: Configure in Program.cs
```csharp
builder.Services.AddDbContext<YourDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    options.AddDebugDashboardInterceptor();
});
```

## 📝 Custom Logging

### Using DebugLogger
```csharp
public class HomeController : Controller
{
    private readonly IDebugLogger _debugLogger;
    
    public HomeController(IDebugLogger debugLogger)
    {
        _debugLogger = debugLogger;
    }
    
    public IActionResult Index()
    {
        // Log custom messages
        _debugLogger.LogInfo("User accessed homepage");
        
        _debugLogger.LogWarning("This is a warning message", new { UserId = 123 });
        
        _debugLogger.LogError("Something went wrong", new { 
            ErrorCode = "E001",
            Details = "Additional error details" 
        });
        
        return View();
    }
}
```

### Static Logger
```csharp
public class SomeService
{
    public void DoSomething()
    {
        // Static usage
        DebugLogger.Log("Processing started", "info", new { ProcessId = Guid.NewGuid() });
        
        try
        {
            // Your code here
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"Error in DoSomething: {ex.Message}", "error");
        }
    }
}
```

## 🔒 Security Considerations

### 1. Environment-Based Enabling
```csharp
builder.Services.AddDebugDashboard(options =>
{
    // Only enable in development
    options.IsEnabled = builder.Environment.IsDevelopment();
    
    // Or use configuration
    options.IsEnabled = builder.Configuration.GetValue<bool>("DebugDashboard:Enabled");
});
```

### 2. Exclude Sensitive Data
```csharp
builder.Services.AddDebugDashboard(options =>
{
    options.ExcludedHeaders = new[] 
    { 
        "Authorization", 
        "Cookie", 
        "X-API-Key",
        "X-Auth-Token"
    };
    
    options.ExcludedPaths = new[] 
    { 
        "/admin", 
        "/api/sensitive",
        "/health"
    };
    
    // Don't log request/response bodies for sensitive endpoints
    options.LogRequestBodies = false;
    options.LogResponseBodies = false;
});
```

### 3. Production Considerations
```csharp
// For production debugging (use with caution)
builder.Services.AddDebugDashboard(options =>
{
    options.IsEnabled = builder.Configuration.GetValue<bool>("DebugDashboard:Enabled");
    options.LogRequestBodies = false; // Disable in production
    options.LogResponseBodies = false; // Disable in production
    options.MaxEntries = 1000; // Limit entries
    options.RetentionPeriod = TimeSpan.FromHours(6); // Short retention
    options.AllowDataExport = false; // Disable export
});
```

## 📊 Performance Optimization

### 1. Limit Data Collection
```csharp
builder.Services.AddDebugDashboard(options =>
{
    options.MaxEntries = 5000; // Limit total entries
    options.MaxBodySize = 64 * 1024; // 64KB max body size
    options.SlowQueryThresholdMs = 500; // Only log slow queries
    options.SlowRequestThresholdMs = 1000; // Only log slow requests
});
```

### 2. Background Cleanup
```csharp
// Add background service for cleanup
builder.Services.AddHostedService<DebugDashboardCleanupService>();
```

## 🚀 Deployment

### 1. Docker Configuration
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["YourApp.csproj", "."]
RUN dotnet restore "YourApp.csproj"
COPY . .
RUN dotnet build "YourApp.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "YourApp.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create directory for debug dashboard database
RUN mkdir -p /app/data
ENV DebugDashboard__DatabasePath=/app/data/debug-dashboard.db

ENTRYPOINT ["dotnet", "YourApp.dll"]
```

### 2. appsettings.json
```json
{
  "DebugDashboard": {
    "Enabled": false,
    "DatabasePath": "./data/debug-dashboard.db",
    "MaxEntries": 10000,
    "LogRequestBodies": false,
    "LogResponseBodies": false,
    "RetentionPeriod": "1.00:00:00",
    "ExcludedPaths": ["/health", "/metrics"],
    "ExcludedHeaders": ["Authorization", "Cookie"]
  }
}
```

### 3. Environment Variables
```bash
# Enable debug dashboard
export DebugDashboard__Enabled=true

# Set database path
export DebugDashboard__DatabasePath=/app/data/debug-dashboard.db

# Configure retention
export DebugDashboard__RetentionPeriod=06:00:00
```

## 🔄 Real-time Updates

### Enable SignalR (Optional)
```csharp
builder.Services.AddSignalR();
builder.Services.AddDebugDashboard(options =>
{
    options.EnableRealTimeUpdates = true;
});

// In Configure method
app.MapHub<DebugDashboardHub>("/_debug/hub");
```

## 📈 Monitoring and Alerts

### 1. Health Checks
```csharp
builder.Services.AddHealthChecks()
    .AddCheck<DebugDashboardHealthCheck>("debug-dashboard");
```

### 2. Export Data
```csharp
// Programmatically export data
public class ExportService
{
    private readonly IDebugStorage _storage;
    
    public async Task ExportDataAsync()
    {
        var data = await _storage.ExportAllAsync();
        // Save to file, send to monitoring system, etc.
    }
}
```

## 🎛️ Dashboard Features

### Available Endpoints
- `/_debug` - Main dashboard
- `/_debug/api/stats` - Statistics API
- `/_debug/api/requests` - HTTP requests
- `/_debug/api/queries` - SQL queries
- `/_debug/api/logs` - Log entries
- `/_debug/api/exceptions` - Exceptions
- `/_debug/api/export` - Export data
- `/_debug/api/health` - Health check
- `/_debug/api/performance` - Performance metrics

### Dashboard Features
- **Dark/Light Mode** - Toggle between themes
- **Real-time Updates** - Auto-refresh data
- **Search & Filter** - Find specific entries
- **Export Data** - Download as JSON
- **Performance Metrics** - Response time analysis
- **Exception Tracking** - Detailed error information
- **SQL Query Analysis** - Performance insights
- **Request/Response Inspection** - Full HTTP details

## 🐛 Troubleshooting

### Common Issues

1. **Dashboard not accessible**
   - Check that `IsEnabled = true`
   - Verify middleware order
   - Ensure running in development or `forceEnable = true`

2. **No data appearing**
   - Check database permissions
   - Verify middleware is registered
   - Check excluded paths configuration

3. **Performance issues**
   - Reduce `MaxEntries`
   - Disable body logging
   - Increase cleanup frequency

4. **Database errors**
   - Check database file permissions
   - Verify disk space
   - Check database path configuration

### Debug Commands
```bash
# Check database
sqlite3 debug-dashboard.db ".tables"

# View configuration
curl https://localhost:5001/_debug/api/config

# Check health
curl https://localhost:5001/_debug/api/health
```

## 🏷️ Versioning

Current version: `1.0.0-preview1`

### Breaking Changes
- None yet (preview release)

### Upgrade Guide
- No upgrades available yet

## 📄 License

MIT License - see LICENSE file for details.
