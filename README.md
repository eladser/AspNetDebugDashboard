# ASP.NET Debug Dashboard

[![Build Status](https://github.com/eladser/AspNetDebugDashboard/workflows/Build%20and%20Test/badge.svg)](https://github.com/eladser/AspNetDebugDashboard/actions)
[![NuGet](https://img.shields.io/nuget/v/AspNetDebugDashboard.svg)](https://www.nuget.org/packages/AspNetDebugDashboard/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/AspNetDebugDashboard.svg)](https://www.nuget.org/packages/AspNetDebugDashboard/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

🔍 **A lightweight, developer-friendly debugging dashboard for ASP.NET Core apps inspired by Laravel Telescope**

Transform your debugging experience with real-time insights into HTTP requests, database queries, logs, and exceptions - all in a beautiful, modern interface.

## ✨ Features

### 🌐 **HTTP Request Monitoring**
- Real-time request tracking with method, path, and status codes
- Request/response body capture and inspection
- Headers and query parameters logging
- Performance metrics and slow request detection
- IP address and user agent tracking

### 🗃️ **SQL Query Analysis**
- Entity Framework Core query interception
- SQL query text with parameters
- Execution time tracking and slow query detection
- Query success/failure status
- Performance insights and optimization suggestions

### 🚨 **Exception Tracking**
- Global exception handling and logging
- Full stack traces with line numbers
- Exception type classification
- Request context and route information
- Error frequency and patterns

### 📝 **Custom Logging**
- Structured logging with tags and categories
- Custom properties and context data
- Log levels (Info, Warning, Error, Success)
- Searchable and filterable log entries
- Integration with ASP.NET Core logging

### 🎨 **Modern Dashboard**
- **Dark/Light Mode** - Toggle between beautiful themes
- **Real-time Updates** - Live data refresh without page reload
- **Advanced Search** - Find specific entries across all data types
- **Export/Import** - Download data as JSON for analysis
- **Performance Charts** - Visual insights into application performance
- **Responsive Design** - Works perfectly on all screen sizes

## 🚀 Quick Start

### 1. Install Package
```bash
dotnet add package AspNetDebugDashboard
```

### 2. Configure Services
```csharp
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
});

// Add EF Core with Debug Dashboard
builder.Services.AddDbContext<YourDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    options.AddDebugDashboardInterceptor();
});

var app = builder.Build();

// Use Debug Dashboard middleware
app.UseDebugDashboard();

app.Run();
```

### 3. Access Dashboard
Navigate to `https://localhost:5001/_debug` 🎉

## 🎯 Advanced Configuration

```csharp
builder.Services.AddDebugDashboard(options =>
{
    // Basic settings
    options.IsEnabled = true;
    options.MaxEntries = 10000;
    options.DatabasePath = "debug-dashboard.db";
    
    // Request/Response logging
    options.LogRequestBodies = true;
    options.LogResponseBodies = true;
    options.MaxBodySize = 1024 * 1024; // 1MB
    
    // SQL query logging
    options.LogSqlQueries = true;
    options.SlowQueryThresholdMs = 1000;
    options.EnableDetailedSqlLogging = true;
    
    // Exception handling
    options.LogExceptions = true;
    options.EnableStackTraceCapture = true;
    
    // Performance monitoring
    options.EnablePerformanceCounters = true;
    options.SlowRequestThresholdMs = 2000;
    
    // Security
    options.ExcludedPaths = new[] { "/health", "/metrics" };
    options.ExcludedHeaders = new[] { "Authorization", "Cookie" };
    
    // Data management
    options.AllowDataExport = true;
    options.RetentionPeriod = TimeSpan.FromDays(7);
});
```

## 💡 Usage Examples

### Custom Logging
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
        _debugLogger.LogInfo("User accessed homepage");
        
        _debugLogger.LogWarning("Potential issue detected", new { 
            UserId = 123,
            Action = "Homepage Visit"
        });
        
        return View();
    }
}
```

### Static Logging
```csharp
public class SomeService
{
    public void ProcessData()
    {
        DebugLogger.Log("Processing started", "info");
        
        try
        {
            // Your processing logic
            DebugLogger.Log("Processing completed successfully", "success");
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"Processing failed: {ex.Message}", "error");
        }
    }
}
```

## 📊 Dashboard Features

### 🏠 **Dashboard Overview**
- Real-time statistics and metrics
- Slowest requests and recent exceptions
- Performance trends and insights
- Quick access to all data types

### 🔍 **Smart Search & Filtering**
- Global search across all data types
- Filter by date range, status codes, methods
- Advanced query builders
- Saved search preferences

### 📈 **Performance Analytics**
- Response time percentiles (P95, P99)
- Request volume and error rates
- Slowest endpoints identification
- Performance trend analysis

### 📤 **Data Export**
- JSON export of all captured data
- Filtered data export options
- Integration with external tools
- Backup and restore capabilities

## 🔧 API Endpoints

The dashboard provides RESTful APIs for programmatic access:

- `GET /_debug/api/stats` - Overall statistics
- `GET /_debug/api/requests` - HTTP request data
- `GET /_debug/api/queries` - SQL query information
- `GET /_debug/api/logs` - Log entries
- `GET /_debug/api/exceptions` - Exception details
- `GET /_debug/api/export` - Export all data
- `GET /_debug/api/performance` - Performance metrics
- `GET /_debug/api/health` - Health check

## 🏗️ Architecture

```
AspNetDebugDashboard/
├── Core/
│   ├── Models/          # Data models and configurations
│   └── Services/        # Business logic and interfaces
├── Middleware/
│   ├── DebugRequestMiddleware.cs    # HTTP request capture
│   └── DebugExceptionMiddleware.cs  # Exception handling
├── Interceptors/
│   └── DebugCommandInterceptor.cs   # EF Core SQL capture
├── Storage/
│   └── LiteDbStorage.cs             # LiteDB data persistence
├── Web/
│   ├── Controllers/                 # API controllers
│   └── Views/                       # React dashboard
└── Extensions/
    └── ServiceCollectionExtensions.cs # DI registration
```

## 🔒 Security Considerations

- **Development Only**: Disabled by default in production
- **Data Sanitization**: Excludes sensitive headers and paths
- **Configurable Privacy**: Control what data is captured
- **Local Storage**: All data stored locally, no external dependencies
- **Minimal Footprint**: Lightweight with minimal performance impact

## 🤝 Contributing

We welcome contributions! Please read our [Contributing Guide](CONTRIBUTING.md) for details on:

- Code of conduct
- Development setup
- Pull request process
- Issue reporting guidelines

## 📚 Documentation

- [**Setup Guide**](docs/SETUP.md) - Detailed installation and configuration
- [**API Documentation**](docs/API.md) - Complete API reference
- [**Configuration Reference**](docs/CONFIGURATION.md) - All configuration options
- [**Troubleshooting**](docs/TROUBLESHOOTING.md) - Common issues and solutions

## 🎖️ Why Choose ASP.NET Debug Dashboard?

- **🚀 Zero Configuration**: Works out of the box with sensible defaults
- **🎨 Beautiful UI**: Modern, responsive interface with dark/light themes
- **⚡ High Performance**: Minimal impact on application performance
- **🔧 Highly Configurable**: Customize every aspect to your needs
- **🛡️ Security Focused**: Built with security best practices
- **📱 Mobile Friendly**: Full responsive design for all devices
- **🔄 Real-time**: Live updates without page refresh
- **💾 Persistent**: Data survives application restarts

## 🗺️ Roadmap

- [ ] SignalR integration for real-time updates
- [ ] Plugin architecture for custom data sources
- [ ] Integration with popular logging frameworks
- [ ] Advanced analytics and reporting
- [ ] Performance profiling and APM features
- [ ] Multi-tenant support
- [ ] Custom dashboard themes and layouts

## 📦 NuGet Package

```xml
<PackageReference Include="AspNetDebugDashboard" Version="1.0.0" />
```

## 🏷️ Versioning

This project uses [Semantic Versioning](https://semver.org/). For available versions, see the [tags on this repository](https://github.com/eladser/AspNetDebugDashboard/tags).

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Inspired by [Laravel Telescope](https://laravel.com/docs/telescope)
- Built with love for the ASP.NET Core community
- Special thanks to all contributors and users

---

<div align="center">

**[⭐ Star this repository](https://github.com/eladser/AspNetDebugDashboard/stargazers)** if you find it helpful!

Made with ❤️ by [eladser](https://github.com/eladser)

</div>
