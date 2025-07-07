# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0-preview1] - 2025-01-07

### Added
- Initial release of AspNetDebugDashboard
- HTTP request logging and monitoring
- SQL query capture via Entity Framework Core interceptors
- Exception logging with detailed stack traces
- Custom log entries with tags and properties
- Web-based dashboard with React frontend
- LiteDB storage for lightweight, embedded database
- Real-time statistics and performance metrics
- Configurable logging options
- Sample application demonstrating usage
- Comprehensive unit tests
- Complete documentation and README

### Features
- **Request Logging**: Captures HTTP method, URL, headers, status codes, and execution time
- **SQL Query Monitoring**: Logs all EF Core queries with parameters and execution time
- **Exception Handling**: Global exception capture with detailed error information
- **Custom Logging**: Helper methods for structured logging with tags and properties
- **Web Dashboard**: Clean, responsive interface built with React and Tailwind CSS
- **Performance Stats**: Real-time metrics including slowest requests and queries
- **Filtering**: Advanced filtering options for all logged data
- **Easy Integration**: Simple setup with minimal configuration required
- **Development Focus**: Designed for development environments with safety controls

### Technical Details
- Built for .NET 7.0+
- Uses LiteDB for embedded storage
- React frontend with Tailwind CSS
- Entity Framework Core interceptors for SQL logging
- Middleware-based request/response capture
- Dependency injection integration
- Background cleanup services
- Configurable data retention policies

### Getting Started
1. Install the NuGet package: `dotnet add package AspNetDebugDashboard`
2. Add services: `builder.Services.AddDebugDashboard()`
3. Use middleware: `app.UseDebugDashboard()`
4. Navigate to: `https://localhost:5001/_debug`

### Known Limitations
- Development environment only by default
- No authentication/authorization (localhost only)
- Limited to LiteDB storage in this version
- No real-time updates via SignalR (planned for future release)

### Dependencies
- Microsoft.AspNetCore.OpenApi (7.0.0)
- Microsoft.EntityFrameworkCore (7.0.0)
- Microsoft.EntityFrameworkCore.Diagnostics (7.0.0)
- LiteDB (5.0.17)
- Microsoft.Extensions.FileProviders.Embedded (7.0.0)
- Microsoft.AspNetCore.SpaServices.Extensions (7.0.0)
