# ASP.NET Debugging Dashboard

🔍 **A lightweight, developer-friendly debugging dashboard for ASP.NET Core apps inspired by Laravel Telescope**

## Overview

This project provides a real-time debugging dashboard for ASP.NET Core applications, displaying HTTP requests, database queries, logs, and exceptions during development. It's designed to be lightweight, easy to integrate, and developer-friendly.

## Features

- **HTTP Request Logging**: Capture and display all incoming HTTP requests with headers, execution time, and status codes
- **SQL Query Capture**: Log all Entity Framework Core queries with parameters and execution time
- **Exception Logging**: Global exception handling with stack traces and route information
- **Custom Log Entries**: Helper methods for custom logging with tags and categories
- **Web Dashboard**: Clean, responsive interface for viewing all captured data
- **Real-time Updates**: Live updates of new requests and logs

## Tech Stack

- **Backend**: ASP.NET Core 7+
- **Frontend**: React + Tailwind CSS
- **Database**: LiteDB (embedded)
- **Packaging**: NuGet

## Quick Start

1. Install the NuGet package:
```bash
dotnet add package AspNetDebugDashboard
```

2. Add to your `Program.cs`:
```csharp
using AspNetDebugDashboard;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddDebugDashboard();

var app = builder.Build();

// Use middleware (development only)
if (app.Environment.IsDevelopment())
{
    app.UseDebugDashboard();
}

app.Run();
```

3. Navigate to `https://localhost:5001/_debug` to view the dashboard

## Project Structure

```
AspNetDebugDashboard/
├── src/
│   ├── AspNetDebugDashboard/          # Main NuGet package
│   │   ├── Core/                      # Core services and models
│   │   ├── Middleware/                # HTTP request middleware
│   │   ├── Interceptors/              # EF Core interceptors
│   │   ├── Storage/                   # LiteDB storage layer
│   │   ├── Web/                       # Dashboard controllers
│   │   └── Extensions/                # Service extensions
│   └── AspNetDebugDashboard.Web/      # React frontend
├── samples/
│   └── SampleApp/                     # Sample ASP.NET Core app
└── tests/
    └── AspNetDebugDashboard.Tests/    # Unit tests
```

## Development

This project is in active development. Contributions are welcome!

## License

MIT License - see [LICENSE](LICENSE) for details.
