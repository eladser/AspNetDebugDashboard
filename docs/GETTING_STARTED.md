# Getting Started with AspNetDebugDashboard

This guide will help you get up and running with AspNetDebugDashboard in your ASP.NET Core application.

## Installation

### NuGet Package

Install the package via NuGet Package Manager:

```bash
dotnet add package AspNetDebugDashboard
```

Or via Package Manager Console:

```powershell
Install-Package AspNetDebugDashboard
```

## Basic Setup

### 1. Configure Services

In your `Program.cs` file, add the debug dashboard services:

```csharp
using AspNetDebugDashboard.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add your other services
builder.Services.AddControllers();
builder.Services.AddDbContext<YourDbContext>(options => 
    options.UseSqlServer(connectionString));

// Add Debug Dashboard
builder.Services.AddDebugDashboard();

var app = builder.Build();
```

### 2. Configure Middleware

Add the debug dashboard middleware to your request pipeline:

```csharp
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Add Debug Dashboard middleware
    app.UseDebugDashboard();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

### 3. Access the Dashboard

Run your application and navigate to:

```
https://localhost:5001/_debug
```

The dashboard will display real-time information about your application's HTTP requests, SQL queries, logs, and exceptions.

## Advanced Configuration

### Custom Configuration

You can customize the dashboard behavior by providing configuration options:

```csharp
builder.Services.AddDebugDashboard(config =>
{
    config.IsEnabled = true;
    config.LogRequestBodies = true;
    config.LogResponseBodies = false;
    config.LogSqlQueries = true;
    config.LogExceptions = true;
    config.MaxEntries = 2000;
    config.BasePath = "/_debug";
    config.MaxBodySize = 1024 * 1024; // 1MB
    config.ExcludedPaths = new List<string> { "/_debug", "/favicon.ico", "/swagger" };
    config.ExcludedHeaders = new List<string> { "Authorization", "Cookie" };
});
```

### Configuration via appsettings.json

You can also configure the dashboard through your `appsettings.json`:

```json
{
  "DebugDashboard": {
    "IsEnabled": true,
    "LogRequestBodies": true,
    "LogResponseBodies": false,
    "LogSqlQueries": true,
    "LogExceptions": true,
    "MaxEntries": 2000,
    "BasePath": "/_debug",
    "MaxBodySize": 1048576,
    "ExcludedPaths": ["/_debug", "/favicon.ico", "/swagger"],
    "ExcludedHeaders": ["Authorization", "Cookie"]
  }
}
```

Then bind the configuration:

```csharp
builder.Services.Configure<DebugConfiguration>(builder.Configuration.GetSection("DebugDashboard"));
builder.Services.AddDebugDashboard();
```

## Entity Framework Integration

### Automatic SQL Query Logging

To capture SQL queries from Entity Framework Core, configure your DbContext:

```csharp
builder.Services.AddDbContext<YourDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    // Add the debug dashboard interceptor
    options.AddDebugDashboard(builder.Services.BuildServiceProvider());
});
```

### Manual Configuration

Alternatively, you can manually add the interceptor:

```csharp
builder.Services.AddDebugDashboardEntityFramework();

builder.Services.AddDbContext<YourDbContext>((serviceProvider, options) =>
{
    options.UseSqlServer(connectionString);
    options.AddDebugDashboard(serviceProvider);
});
```

## Custom Logging

### Using the Static Logger

Use the static `DebugLogger` class to add custom log entries:

```csharp
using AspNetDebugDashboard;

// In your controller or service
public async Task<IActionResult> MyAction()
{
    await DebugLogger.InfoAsync("Processing user request", "MyController");
    
    try
    {
        // Your business logic
        var result = await ProcessDataAsync();
        
        await DebugLogger.SuccessAsync("Data processed successfully", "MyController", 
            new Dictionary<string, object> { { "RecordCount", result.Count } });
        
        return Ok(result);
    }
    catch (Exception ex)
    {
        await DebugLogger.ErrorAsync($"Error processing data: {ex.Message}", "MyController");
        throw;
    }
}
```

### Using Dependency Injection

Inject the `IDebugLogger` service:

```csharp
public class MyService
{
    private readonly IDebugLogger _debugLogger;
    
    public MyService(IDebugLogger debugLogger)
    {
        _debugLogger = debugLogger;
    }
    
    public async Task ProcessAsync()
    {
        await _debugLogger.LogAsync("Starting process", "Info", "MyService");
        
        // Your logic here
        
        await _debugLogger.LogAsync("Process completed", "Success", "MyService");
    }
}
```

## Dashboard Features

### Dashboard Overview

The main dashboard provides:
- Real-time statistics
- Request/response metrics
- SQL query performance
- Exception tracking
- Custom log entries

### Requests Tab

View all HTTP requests with:
- HTTP method and path
- Response status codes
- Execution time
- Request/response headers
- Request/response bodies (if enabled)

### SQL Queries Tab

Monitor database queries with:
- SQL query text
- Query parameters
- Execution time
- Success/failure status
- Associated request context

### Logs Tab

View custom log entries with:
- Log level (Info, Warning, Error, Success)
- Log message
- Tags and categories
- Custom properties
- Request association

### Exceptions Tab

Track application exceptions with:
- Exception type and message
- Full stack trace
- Request context
- Inner exception details

## Performance Considerations

### Development Only

The debug dashboard is designed for development environments. It's automatically disabled in production unless explicitly enabled.

### Storage Management

The dashboard uses LiteDB for storage with automatic cleanup:
- Configurable maximum entries
- Automatic old entry removal
- Background cleanup service

### Memory Usage

To minimize memory usage:
- Limit body logging size
- Exclude unnecessary paths
- Configure appropriate retention policies

## Security Considerations

### Development Environment

By default, the dashboard only runs in development environments.

### Sensitive Data

The dashboard automatically excludes sensitive headers like:
- Authorization
- Cookie
- Custom API keys

Configure additional exclusions as needed:

```csharp
config.ExcludedHeaders = new List<string> 
{ 
    "Authorization", 
    "Cookie", 
    "X-API-Key",
    "X-Custom-Secret"
};
```

### Path Exclusions

Exclude paths that shouldn't be logged:

```csharp
config.ExcludedPaths = new List<string>
{
    "/_debug",
    "/favicon.ico",
    "/health",
    "/metrics"
};
```

## Troubleshooting

### Dashboard Not Accessible

1. Ensure the middleware is added: `app.UseDebugDashboard()`
2. Check if running in development environment
3. Verify the base path configuration
4. Check for port conflicts

### SQL Queries Not Appearing

1. Verify EF Core interceptor is added
2. Check if `LogSqlQueries` is enabled
3. Ensure DbContext is properly configured
4. Verify database operations are being performed

### High Memory Usage

1. Reduce `MaxEntries` setting
2. Disable body logging if not needed
3. Add more path exclusions
4. Reduce `MaxBodySize`

## Next Steps

- Explore the [sample application](../samples/SampleApp/) for complete examples
- Check the [API documentation](API.md) for advanced usage
- Review [configuration options](CONFIGURATION.md) for customization
- See [troubleshooting guide](TROUBLESHOOTING.md) for common issues
