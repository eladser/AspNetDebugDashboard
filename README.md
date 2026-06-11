<p align="center">
  <img src="docs/images/banner.png" alt="" width="780">
</p>

# ASP.NET Debug Dashboard

[![CI](https://github.com/eladser/AspNetDebugDashboard/actions/workflows/ci.yml/badge.svg)](https://github.com/eladser/AspNetDebugDashboard/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/AspNetDebugDashboard.svg)](https://www.nuget.org/packages/AspNetDebugDashboard/)
[![Downloads](https://img.shields.io/nuget/dt/AspNetDebugDashboard.svg)](https://www.nuget.org/packages/AspNetDebugDashboard/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Request, SQL query, log, and exception capture for ASP.NET Core, viewable in a dashboard at `/_debug`. Think Laravel Telescope, but for .NET.

Everything is stored locally in a LiteDB file. The dashboard ships inside the package as a single self-contained page, so there are no CDN dependencies and it works offline.

![Demo](docs/images/demo.gif)

## Install

```bash
dotnet add package AspNetDebugDashboard
```

Or in the project file:

```xml
<PackageReference Include="AspNetDebugDashboard" Version="2.0.0" />
```

Or from the Package Manager Console in Visual Studio:

```powershell
Install-Package AspNetDebugDashboard
```

Works on .NET 8, 9, and 10. No other setup files, schemas, or services needed — storage is an embedded LiteDB database created on first run.

## Setup

The minimum is two lines:

```csharp
using AspNetDebugDashboard.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDebugDashboard();          // 1. register services

var app = builder.Build();

app.UseDebugDashboard();                       // 2. add middleware (no-op outside Development)

app.MapControllers();
app.Run();
```

Run your app and open **`/_debug`**.

### Capture EF Core queries

Attach the interceptor when registering your context:

```csharp
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseSqlServer(connectionString);    // any relational provider
    options.AddDebugDashboard(sp);
});
```

Works with SQL Server, PostgreSQL, SQLite, MySQL — anything that goes through EF Core's relational pipeline. (`UseInMemoryDatabase` produces no SQL, so there's nothing to capture there.)

### Write your own log entries

Inject `IDebugLogger` anywhere:

```csharp
public class OrderService(IDebugLogger log)
{
    public async Task<Order> CreateAsync(CreateOrderRequest req)
    {
        await log.LogInfoAsync("Creating order", properties: new() { ["customerId"] = req.CustomerId });
        // LogWarningAsync, LogErrorAsync, LogSuccessAsync, or LogAsync(message, level)
    }
}
```

Or use the static logger where injection is awkward:

```csharp
await DebugLogger.InfoAsync("Cache warmed", tag: "startup");
```

Entries written during a request are attached to it, so the request detail shows the logs and queries it produced.

## The dashboard

| | |
|---|---|
| **Overview** | totals, error rate, status/method distribution, slowest requests and queries |
| **Performance** | req/min, avg, median, P95/P99, error rate, slowest endpoints — last hour |
| **Requests** | sortable table with duration bars; detail has Summary / Headers / Request / Response / SQL / Logs tabs and a **copy-as-cURL** button |
| **Queries** | full SQL with syntax highlighting, parameters, timing; slow queries flagged; an **N+1 warning** appears when one request runs the same query 3+ times |
| **Logs** | level, category, structured properties, stack traces |
| **Exceptions** | type, message, full stack trace, inner exceptions, the route that threw |

Global search (`Ctrl+K`) covers everything. Tables navigate from the keyboard: `j`/`k` rows, `Enter` open, `/` filter, `Esc` close. Queries, logs, and exceptions link back to their parent request.

![Overview](docs/images/overview.png)

![Request detail](docs/images/request-detail.png)

![Query detail](docs/images/query-detail.png)

## Configuration

All options, with their defaults:

```csharp
builder.Services.AddDebugDashboard(options =>
{
    options.BasePath = "/_debug";              // dashboard route
    options.DatabasePath = "debug-dashboard.db";
    options.MaxEntries = 1000;                 // per entry type, oldest trimmed first
    options.LogRequestBodies = true;
    options.LogResponseBodies = false;
    options.MaxBodySize = 1024 * 1024;         // bodies above this are skipped
    options.SlowQueryThresholdMs = 1000;
    options.ExcludedPaths = new() { "/_debug", "/health" };
    options.ExcludedHeaders = new() { "Authorization", "Cookie" };
    options.RetentionPeriod = TimeSpan.FromDays(7);
});
```

Or bind from `appsettings.json`:

```json
{
  "DebugDashboard": {
    "MaxEntries": 2000,
    "LogResponseBodies": true,
    "SlowQueryThresholdMs": 500
  }
}
```

```csharp
builder.Services.Configure<DebugConfiguration>(builder.Configuration.GetSection("DebugDashboard"));
builder.Services.AddDebugDashboard();
```

The full reference is in [docs/CONFIGURATION.md](docs/CONFIGURATION.md).

## Production

`UseDebugDashboard()` does nothing unless the environment is Development, so leaving the package referenced in production builds is safe. If you do want it on elsewhere (a staging box, say), opt in explicitly:

```csharp
app.UseDebugDashboard(forceEnable: true);
```

If you force-enable it anywhere reachable from the internet, put it behind your own auth. The dashboard itself has none, and captured request bodies can contain anything your users send.

## REST API

The dashboard is a client of a plain JSON API you can also call directly:

| Endpoint | What it returns |
|---|---|
| `GET /_debug/api/stats` | totals and distributions |
| `GET /_debug/api/requests` | paged requests — supports `search`, `method`, `statusCode`, `isSuccessful`, `minExecutionTime`, `sortBy`, `page` |
| `GET /_debug/api/queries` | paged SQL queries — supports `search`, `isSlowQuery`, `isSuccessful` |
| `GET /_debug/api/logs` | paged logs — supports `search`, `level` |
| `GET /_debug/api/exceptions` | paged exceptions |
| `GET /_debug/api/performance` | last-hour metrics (P95/P99, error rate, slowest endpoints) |
| `GET /_debug/api/search?term=` | cross-type search |
| `GET /_debug/api/export` | everything as a JSON file |
| `POST /_debug/api/logs` | write a log entry over HTTP |
| `DELETE /_debug/api/clear` | wipe all captured data |

Full details in [docs/API.md](docs/API.md).

## Try it

The repo ships a sample app with endpoints for generating traffic, slow operations, and test exceptions:

```bash
git clone https://github.com/eladser/AspNetDebugDashboard
cd AspNetDebugDashboard
dotnet run --project samples/SampleApp --urls http://localhost:5000
# hit a few endpoints, then open http://localhost:5000/_debug
curl http://localhost:5000/api/products
curl http://localhost:5000/api/products/slow-operation
curl http://localhost:5000/api/products/test-error
```

## How the dashboard is built

The UI is a Vite + React app in [dashboard/](dashboard/), compiled to one HTML file with everything inlined and embedded into the assembly. To work on it:

```bash
cd dashboard
npm install
npm run dev    # proxies /_debug/api to localhost:5000 — run the sample app alongside
npm run build  # writes src/AspNetDebugDashboard/wwwroot/index.html
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for the full dev loop.

## License

MIT. See [LICENSE](LICENSE).
