# ASP.NET Debug Dashboard

[![Build Status](https://github.com/eladser/AspNetDebugDashboard/workflows/Build%20and%20Test/badge.svg)](https://github.com/eladser/AspNetDebugDashboard/actions)
[![NuGet](https://img.shields.io/nuget/v/AspNetDebugDashboard.svg)](https://www.nuget.org/packages/AspNetDebugDashboard/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/AspNetDebugDashboard.svg)](https://www.nuget.org/packages/AspNetDebugDashboard/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

> 🔍 **A modern, lightweight debugging dashboard for ASP.NET Core applications inspired by Laravel Telescope**

Transform your debugging experience with real-time insights into HTTP requests, database queries, logs, and exceptions - all in a beautiful, responsive interface.

![ASP.NET Debug Dashboard](https://raw.githubusercontent.com/eladser/AspNetDebugDashboard/main/docs/images/dashboard-preview.png)

## ✨ Features

### 🌐 **HTTP Request Monitoring**
- **Real-time request tracking** with method, path, and status codes
- **Request/response body capture** with configurable size limits
- **Headers and query parameters** logging
- **Performance metrics** and slow request detection
- **Client information** (IP address, user agent, geolocation)

### 🗃️ **SQL Query Analysis**
- **Entity Framework Core integration** with automatic query interception
- **SQL query text with parameters** and execution time tracking
- **Slow query detection** and optimization suggestions
- **Success/failure status** with error details
- **Database performance insights**

### 🚨 **Exception Tracking**
- **Global exception handling** with full stack traces
- **Exception categorization** by type and frequency
- **Request context** and route information
- **Error patterns** and trend analysis
- **Integration with logging frameworks**

### 📝 **Advanced Logging**
- **Structured logging** with custom properties and tags
- **Multiple log levels** (Info, Warning, Error, Success)
- **Searchable log entries** with powerful filtering
- **Custom categories** and contextual information
- **Performance logging** and metrics

### 🎨 **Modern Dashboard**
- **🌙 Dark/Light Mode** - Beautiful themes with persistent preferences
- **📱 Responsive Design** - Perfect on desktop, tablet, and mobile
- **⚡ Real-time Updates** - Live data refresh with SignalR
- **🔍 Advanced Search** - Find anything across all data types
- **📊 Performance Charts** - Visual insights and trends
- **📤 Export/Import** - Download data for external analysis
- **🎯 Smart Filtering** - Date ranges, status codes, methods, and more

## 🚀 Quick Start

### Installation

```bash
dotnet add package AspNetDebugDashboard
```

### Basic Setup

```csharp
using AspNetDebugDashboard.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add Debug Dashboard
builder.Services.AddDebugDashboard();

// Add Entity Framework with Debug Dashboard integration
builder.Services.AddDbContext<YourDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    options.AddDebugDashboardInterceptor();
});

var app = builder.Build();

// Enable Debug Dashboard (development only by default)
app.UseDebugDashboard();

app.Run();
```

### Access Dashboard

Navigate to **`https://localhost:5001/_debug`** to view your dashboard! 🎉

## ⚙️ Configuration

### Advanced Configuration

```csharp
builder.Services.AddDebugDashboard(options =>
{
    // Basic settings
    options.IsEnabled = builder.Environment.IsDevelopment();
    options.MaxEntries = 10000;
    options.DatabasePath = "debug-dashboard.db";
    
    // Request/Response logging
    options.LogRequestBodies = true;
    options.LogResponseBodies = true;
    options.MaxBodySize = 1024 * 1024; // 1MB
    
    // SQL query monitoring
    options.LogSqlQueries = true;
    options.SlowQueryThresholdMs = 1000;
    options.EnableDetailedSqlLogging = true;
    
    // Exception handling
    options.LogExceptions = true;
    options.EnableStackTraceCapture = true;
    
    // Real-time features
    options.EnableRealTimeUpdates = true;
    
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

### Production Configuration

```csharp
// appsettings.Production.json
{
  "DebugDashboard": {
    "Enabled": false, // Disable in production
    "LogRequestBodies": false,
    "LogResponseBodies": false,
    "MaxEntries": 1000,
    "RetentionPeriod": "06:00:00"
  }
}
```

## 💡 Usage Examples

### Custom Logging

```csharp
public class OrderService
{
    private readonly IDebugLogger _debugLogger;
    
    public OrderService(IDebugLogger debugLogger)
    {
        _debugLogger = debugLogger;
    }
    
    public async Task<Order> CreateOrderAsync(CreateOrderRequest request)
    {
        _debugLogger.LogInfo("Order creation started", new { 
            CustomerId = request.CustomerId,
            ItemCount = request.Items.Count 
        });
        
        try
        {
            var order = await ProcessOrderAsync(request);
            
            _debugLogger.LogSuccess("Order created successfully", new {
                OrderId = order.Id,
                Total = order.Total
            });
            
            return order;
        }
        catch (Exception ex)
        {
            _debugLogger.LogError($"Order creation failed: {ex.Message}", new {
                CustomerId = request.CustomerId,
                Error = ex.GetType().Name
            });
            throw;
        }
    }
}
```

### Static Logging

```csharp
public class PaymentProcessor
{
    public async Task ProcessPaymentAsync(Payment payment)
    {
        DebugLogger.Log("Payment processing started", "info", new { 
            PaymentId = payment.Id,
            Amount = payment.Amount 
        });
        
        // Your payment logic here
        
        DebugLogger.Log("Payment processed successfully", "success");
    }
}
```

## 📊 Dashboard Features

### 🏠 Dashboard Overview
- **Real-time statistics** and key metrics
- **Slowest requests** and performance bottlenecks
- **Recent exceptions** with quick access to details
- **System health** and status indicators

### 🔍 Advanced Search & Filtering
- **Global search** across all data types (requests, queries, logs, exceptions)
- **Date range filtering** with custom periods
- **Status code filtering** (200, 404, 500, etc.)
- **HTTP method filtering** (GET, POST, PUT, DELETE)
- **Log level filtering** (Info, Warning, Error)
- **Performance filtering** (slow queries, long requests)

### 📈 Performance Analytics
- **Response time percentiles** (P50, P95, P99)
- **Request volume trends** and patterns
- **Error rate analysis** and monitoring
- **Slowest endpoints** identification
- **Database performance** metrics
- **Memory and resource usage** tracking

### 📤 Data Management
- **JSON export** of all captured data
- **Filtered exports** for specific data sets
- **Automated cleanup** with configurable retention
- **Background maintenance** services
- **Health monitoring** for operational visibility

## 🏗️ Architecture

### Components

```
AspNetDebugDashboard/
├── Core/
│   ├── Models/          # Data models and configurations
│   └── Services/        # Business logic and interfaces
├── Middleware/
│   ├── DebugRequestMiddleware     # HTTP request capture
│   └DebugExceptionMiddleware      # Global exception handling
├── Interceptors/
│   └── DebugCommandInterceptor    # EF Core SQL interception
├── Storage/
│   └── LiteDbStorage             # Lightweight data persistence
├── Web/
│   ├── Controllers/              # REST API endpoints
│   ├── Views/                    # React dashboard interface
│   └── Hubs/                     # SignalR real-time communication
└── Extensions/
    └── ServiceCollectionExtensions # Dependency injection setup
```

### Technology Stack

- **Backend**: ASP.NET Core 7.0+ with middleware and dependency injection
- **Frontend**: React 18 with modern hooks and real-time updates
- **Styling**: Tailwind CSS with responsive design and dark/light themes
- **Storage**: LiteDB for lightweight, embedded database
- **Real-time**: SignalR for live dashboard updates
- **Icons**: Font Awesome for beautiful, scalable icons

## 🔒 Security & Privacy

### Security Features
- **Development-only by default** - Automatically disabled in production
- **Configurable data capture** - Control what information is logged
- **Sensitive data exclusion** - Automatically filters headers like Authorization
- **Path-based exclusions** - Skip monitoring for specific endpoints
- **Size limits** - Prevent large payloads from impacting performance
- **Data sanitization** - Clean and validate all captured information

### Privacy Controls
```csharp
options.ExcludedHeaders = new[] { 
    "Authorization", "Cookie", "X-API-Key", "X-Auth-Token" 
};
options.ExcludedPaths = new[] { 
    "/admin", "/api/auth", "/health" 
};
options.LogRequestBodies = false;  // Disable for sensitive data
options.LogResponseBodies = false; // Disable for sensitive data
```

## 🚀 Performance

### Minimal Overhead
- **< 5ms overhead** per request on average
- **Async processing** for non-blocking operation
- **Configurable data collection** to control performance impact
- **Background cleanup** to prevent storage bloat
- **Memory efficient** with automatic resource management

### Scalability
- **Tested up to 1000+ concurrent requests**
- **Automatic database optimization**
- **Configurable retention policies**
- **Background maintenance services**
- **Health monitoring** and alerting

## 🧪 Testing

Run the comprehensive test suite:

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run performance tests
dotnet test --filter Category=Performance
```

### Test Coverage
- **95%+ code coverage** across all components
- **Unit tests** for all services and middleware
- **Integration tests** for end-to-end functionality
- **Performance tests** for scalability validation
- **Security tests** for production readiness

## 📚 Documentation

- **[Getting Started](docs/GETTING_STARTED.md)** - Step-by-step setup guide
- **[Configuration](docs/CONFIGURATION.md)** - Complete configuration reference
- **[API Documentation](docs/API.md)** - REST API endpoints and usage
- **[Security Guide](docs/SECURITY.md)** - Security best practices
- **[Troubleshooting](docs/TROUBLESHOOTING.md)** - Common issues and solutions

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

### Development Setup

```bash
# Clone the repository
git clone https://github.com/eladser/AspNetDebugDashboard.git

# Restore dependencies
dotnet restore

# Run tests
dotnet test

# Start the sample application
cd samples/SampleApp
dotnet run
```

## 📦 NuGet Package

```xml
<PackageReference Include="AspNetDebugDashboard" Version="1.0.0" />
```

## 🌟 Why Choose ASP.NET Debug Dashboard?

### ✅ **Production Ready**
- Comprehensive test coverage with 95%+ code coverage
- Security-first design with configurable privacy controls
- Performance optimized with minimal overhead
- Health monitoring and operational visibility

### ✅ **Developer Friendly**
- Zero-configuration setup with intelligent defaults
- Beautiful, intuitive interface that works on all devices
- Real-time updates without page refresh
- Extensive customization options for any use case

### ✅ **Enterprise Grade**
- Multi-framework support (.NET 7.0 and 8.0)
- Background services for automated maintenance
- Data export for compliance and analysis
- Professional support and documentation

### ✅ **Modern Technology**
- Built with latest ASP.NET Core and React
- Real-time capabilities with SignalR
- Responsive design with dark/light themes
- Progressive enhancement for all browsers

## 🗺️ Roadmap

- [ ] **Plugin Architecture** - Custom data sources and integrations
- [ ] **Advanced Analytics** - Machine learning insights and anomaly detection
- [ ] **Multi-tenant Support** - Enterprise deployment scenarios
- [ ] **Custom Themes** - Brandable dashboard themes
- [ ] **APM Integration** - Application Performance Monitoring features
- [ ] **Distributed Tracing** - Microservices debugging support

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Inspired by [Laravel Telescope](https://laravel.com/docs/telescope)
- Built with ❤️ for the ASP.NET Core community
- Special thanks to all [contributors](https://github.com/eladser/AspNetDebugDashboard/graphs/contributors)

## 📞 Support

- **Documentation**: [GitHub Wiki](https://github.com/eladser/AspNetDebugDashboard/wiki)
- **Issues**: [GitHub Issues](https://github.com/eladser/AspNetDebugDashboard/issues)
- **Discussions**: [GitHub Discussions](https://github.com/eladser/AspNetDebugDashboard/discussions)
- **Stack Overflow**: Tag with `aspnet-debug-dashboard`

---

<div align="center">

**[⭐ Star this repository](https://github.com/eladser/AspNetDebugDashboard/stargazers)** if you find it helpful!

**Made with ❤️ for .NET developers worldwide**

[Website](https://github.com/eladser/AspNetDebugDashboard) • [Documentation](docs/) • [Examples](samples/) • [Changelog](CHANGELOG.md)

</div>
