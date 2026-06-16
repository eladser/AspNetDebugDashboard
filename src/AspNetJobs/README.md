# AspNetJobs

[![NuGet](https://img.shields.io/nuget/v/AspNetJobs.svg)](https://www.nuget.org/packages/AspNetJobs/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/eladser/AspNetDebugDashboard/blob/main/LICENSE)

Background jobs that run in-process, with a live inspector at `/_jobs`. Enqueue work, watch it move through pending, running, succeeded, or failed, with timing and the full stack trace when something throws. No Redis, no Hangfire server, no extra infrastructure. Part of the [AspNetDebugDashboard](https://github.com/eladser/AspNetDebugDashboard) suite.

![Jobs](https://raw.githubusercontent.com/eladser/AspNetDebugDashboard/main/docs/images/jobs-demo.gif)

## Install

```bash
dotnet add package AspNetJobs
```

## Setup

```csharp
using AspNetJobs;

builder.Services.AddJobs();   // 1. register + start the runner
var app = builder.Build();
app.UseJobs();                // 2. serve /_jobs (no-op outside Development)
```

## Enqueuing work

Inject `IJobQueue` and hand it a delegate. It runs on a background worker; the call returns immediately.

```csharp
public class ReportsController(IJobQueue jobs) : ControllerBase
{
    [HttpPost("reports/nightly")]
    public IActionResult Nightly()
    {
        jobs.Enqueue("nightly-report", async ct =>
        {
            await BuildReport(ct);
        });
        return Accepted();
    }
}
```

Jobs run one at a time in enqueue order. The `CancellationToken` is signalled on app shutdown.

## What you get

`/_jobs` shows every job with its status, how long it ran, and when it was queued. Failed jobs expand to the full exception and stack trace. The page polls so running jobs update live. "Clear" drops finished records; in-flight jobs are kept.

## Configuration

```csharp
builder.Services.AddJobs(o =>
{
    o.BasePath = "/_jobs";       // dashboard route
    o.DatabasePath = "jobs.db";  // local LiteDB store
    o.MaxRecords = 500;          // oldest records trimmed past this
});
```

## License

MIT.
