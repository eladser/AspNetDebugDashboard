# AspNetVitals

[![NuGet](https://img.shields.io/nuget/v/AspNetVitals.svg)](https://www.nuget.org/packages/AspNetVitals/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/eladser/AspNetDebugDashboard/blob/main/LICENSE)

A live vitals page at `/_vitals`: memory, GC, threads, uptime, runtime, and your registered health checks, all updating in real time. No storage, no config. Part of the [AspNetDebugDashboard](https://github.com/eladser/AspNetDebugDashboard) suite.

![Vitals](https://raw.githubusercontent.com/eladser/AspNetDebugDashboard/main/docs/images/vitals-demo.gif)

## Install

```bash
dotnet add package AspNetVitals
```

## Setup

```csharp
using AspNetVitals;

builder.Services.AddVitals();
var app = builder.Build();
app.UseVitals();   // serve /_vitals (no-op outside Development)
```

## Health checks

If you've registered health checks, they show up automatically with their status, message, and run time:

```csharp
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy())
    .AddSqlServer(connectionString)
    .AddRedis(redisConnection);
```

No health checks registered? The page still shows process metrics and tells you how to add some.

## What you get

`/_vitals` shows a live memory sparkline plus managed and working-set memory, CPU usage, GC collection counts per generation, total allocated bytes, thread and assembly counts, GC mode, uptime, and the runtime/OS. Below that, every registered health check with a color-coded status. The page polls so the numbers move as your app works.

## Configuration

```csharp
builder.Services.AddVitals(o => o.BasePath = "/_vitals");
```

## License

MIT.
