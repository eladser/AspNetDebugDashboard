# OpenTelemetry tracing

The dashboard captures requests and queries locally in LiteDB. From 2.1.0 it also emits each one as an OpenTelemetry span, so the same data can flow into Aspire, Jaeger, or any OTLP backend without giving up the local capture (bodies, parameters, replay).

## How it works

Captured requests and queries are emitted on an `ActivitySource` named `AspNetDebugDashboard`. An `ActivitySource` produces nothing unless a listener is registered for it, so until you add the source to your tracer this does no work and allocates nothing. That makes it safe to leave on, which is why `EmitActivities` defaults to `true`.

Wire it into your existing OpenTelemetry setup:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("AspNetDebugDashboard")
        .AddOtlpExporter());          // or .AddConsoleExporter() to see it locally
```

`AspNetDebugDashboard` is also exposed as `DebugTelemetry.SourceName` if you'd rather not hard-code the string.

## What's on the spans

Request spans (`ActivityKind.Server`, named `{method} {path}`):

| Tag | Example |
|-----|---------|
| `http.request.method` | `GET` |
| `url.path` | `/api/orders` |
| `url.query` | `?page=2` (only if present) |
| `http.response.status_code` | `200` |
| `debug.request_id` | the trace identifier, matches the dashboard |
| `debug.sql_query_count` | how many queries the request ran |

Status is set to `Error` for 5xx responses.

Query spans (`ActivityKind.Client`, named `db.query`):

| Tag | Example |
|-----|---------|
| `db.system` | `sql` |
| `db.query.text` | the SQL |
| `db.namespace` | database name, if known |
| `debug.request_id` | links the query to its request |
| `debug.rows_affected` | rows affected |
| `debug.slow_query` | `true` past the slow threshold |

Failed queries get an `Error` status with the message.

Spans are back-dated to the operation the dashboard already timed, so durations in your backend match what the dashboard shows.

## Outgoing HTTP calls

Outgoing HTTP capture (2.4) doesn't emit spans. The runtime already does: `System.Net.Http` creates a client span for every `HttpClient` request, and `AddHttpClientInstrumentation()` (or `.AddSource("System.Net.Http")` on .NET 9+) sends them to your tracer. A second span from the dashboard would only duplicate it.

What the dashboard does store is the trace id of each call, shown in the Outgoing detail, so you can find the matching span in your backend.

Capture listens to the same diagnostic events the runtime uses for trace propagation. If you set `System.Net.Http.EnableActivityPropagation` to `false` (the AppContext switch, or the `DOTNET_SYSTEM_NET_HTTP_ENABLEACTIVITYPROPAGATION` environment variable), those events stop and nothing shows up in the Outgoing tab. See [TROUBLESHOOTING.md](TROUBLESHOOTING.md#outgoing-http-calls-dont-show-up).

## ASP.NET Core 11

On .NET 11, ASP.NET Core's own hosting activity carries OpenTelemetry semantic-convention tags (`http.request.method`, `url.path`, `http.response.status_code`, `server.address`) natively. Subscribe to the `Microsoft.AspNetCore` source and no instrumentation library is needed. The dashboard's request spans use overlapping names, so queries and filters in your backend work across both.

If you trace ASP.NET Core and add the `AspNetDebugDashboard` source, you get two server spans per request: the framework's and the dashboard's. That's expected. The dashboard's span is useful for `debug.request_id` and `debug.sql_query_count`; if you don't need those, leave the source out or set `EmitActivities = false`.

## Turning it off

```csharp
builder.Services.AddDebugDashboard(o => o.EmitActivities = false);
```

## A note on sensitive data

These spans carry SQL text and request paths. Locally that's the same thing the dashboard already shows, but an OTLP exporter sends it off the machine to wherever your tracing backend lives. That's fine in development. Don't forward these spans from an environment where the query text or paths would be sensitive, and remember the dashboard (and this emission) only run in Development unless you `forceEnable` them.
