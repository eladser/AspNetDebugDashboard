# Troubleshooting

## Dashboard returns 404 at /_debug

Work through these in order:

1. Both registration calls are present:

   ```csharp
   builder.Services.AddDebugDashboard();   // services
   app.UseDebugDashboard();                // middleware
   ```

2. You're running in the **Development** environment. Outside it, `UseDebugDashboard()` does nothing by design. To verify that's the cause:

   ```csharp
   app.UseDebugDashboard(forceEnable: true);
   ```

3. Controllers are mapped. The dashboard and its API are controllers, so the app needs `app.MapControllers()` (and `AddDebugDashboard` must run before `Build()`).

4. If you changed `BasePath`, the dashboard is at that path, not `/_debug`.

## SQL queries aren't captured

1. The interceptor must be attached to your DbContext:

   ```csharp
   builder.Services.AddDbContext<AppDbContext>((sp, options) =>
   {
       options.UseSqlServer(connectionString);
       options.AddDebugDashboard(sp);
   });
   ```

   Note the `(sp, options)` overload: the interceptor needs the service provider.

2. **`UseInMemoryDatabase` produces no SQL.** The EF in-memory provider doesn't go through the relational command pipeline, so there's nothing to intercept. Use SQLite (`UseSqlite("Data Source=dev.db")`) if you want captured queries during local development.

3. `LogSqlQueries` must be true (it is by default).

4. Raw ADO.NET (`SqlCommand` used directly) bypasses EF and isn't captured. Only commands that flow through EF Core's interceptor pipeline are.

## Logs don't appear

Only entries written through this package's logger are captured: `IDebugLogger` (injected) or the static `DebugLogger`. Output from `ILogger<T>` / Serilog / NLog is **not** picked up; that's a different pipeline.

## Outgoing HTTP calls don't show up

1. `CaptureOutgoingHttp` must be true (it is by default).

2. Calls made outside a request (hosted services, startup, timers) are skipped unless `CaptureUnattachedOutgoingHttp = true`.

3. Capture rides on the runtime's HTTP diagnostic events, which only fire while activity propagation is on. If anything sets the `System.Net.Http.EnableActivityPropagation` AppContext switch to `false`, or the `DOTNET_SYSTEM_NET_HTTP_ENABLEACTIVITYPROPAGATION` environment variable to `false` or `0`, nothing is captured. Some tracing setups and container images set it; check your `runtimeconfig.json` and environment.

4. Only `HttpClient` is covered. `HttpWebRequest`, gRPC over a non-`HttpClient` transport, and raw sockets aren't.

If a call shows up with no response body even though `CaptureOutgoingHttpBodies` is on, the caller never read the response (for example `GetStringAsync` throwing on a 4xx before reading it). Request bodies are only captured when they're already in memory (`StringContent`, `JsonContent`, form content); a `StreamContent` body is skipped so capture doesn't consume it.

## Live updates don't work (the list only refreshes every 5 seconds)

The dashboard listens on `/_debug/api/stream` (server-sent events). If that stream can't open within 5 seconds, drops, or goes 45 seconds without a message, it quietly switches to polling every 5 seconds. So the page still works; it's just slower to update. The usual causes:

- **A proxy buffers the response.** SSE needs every write flushed straight to the browser. The endpoint sends `X-Accel-Buffering: no`, which nginx honors, so nginx works without extra config. IIS with ARR, Azure Front Door, Cloudflare, and other CDNs or reverse proxies may hold the response until it ends. Turn response buffering off for the stream path (`/_debug/api/stream`, or under your `BasePath`), or reach the app directly during development.
- **Response compression.** Don't add `text/event-stream` to `ResponseCompressionOptions.MimeTypes` (it isn't there by default). A compressor buffers output until it has a full block, so events arrive late or in bursts.
- **Too many tabs over HTTP/1.1.** Browsers allow six connections per origin on HTTP/1.1, and each open dashboard tab keeps one stream open. With several tabs (plus the rest of the suite, or your app's own pages) new requests can queue behind them. Close some tabs, or serve over HTTPS where Kestrel negotiates HTTP/2 and the limit doesn't apply.
- `EnableRealTimeUpdates` is false. The endpoint then returns 404 and the UI polls, which is the intended behavior.

To check the stream directly:

```bash
curl -N http://localhost:5000/_debug/api/stream
```

You should see `retry: 5000` at once, then an `event: entry` line whenever something is captured, and `event: ping` every 15 seconds when it's idle. If lines only show up in chunks, something between you and the app is buffering.

## Queries/logs aren't attached to their request

Entries are correlated by `HttpContext.TraceIdentifier` while the request is in flight. Work done outside a request (background services, startup seeding, hosted jobs) is still captured, but lands unattached, so you'll see it in the Queries/Logs tabs with no parent request link.

## The Timeline tab is missing or a bar looks wrong

- The tab only shows when at least one SQL query, outgoing call, log, or dump in the request has a start offset. Requests captured before 2.5 have none, and neither do requests that didn't run anything the dashboard captures. Those open on Summary.
- Offsets are only recorded while the request is in flight. Something that finishes after the request was recorded (fire-and-forget work, say) gets no offset and isn't drawn.
- SQL bars show command time. For a query that returns rows, the bar ends when EF Core gets the data reader back; reading the rows into entities happens after that and isn't part of it. A query that returns a lot of rows can have a short bar and a gap after it.
- Outgoing HTTP bars end when the response headers arrive. `HttpClient` reads the body after that, so a large download takes longer than its bar shows.
- Logs and dumps are points in time and show as ticks. An unhandled exception is drawn at the end of the request, since there's no offset for it.
- With more than 300 entries, the first 300 are drawn and a "show all" link renders the rest.

## Replay doesn't match the original request

Replay rebuilds the request from what was captured and sends it with `fetch()` from the dashboard page, to the dashboard's own origin. Some things don't survive that:

- **Antiforgery tokens.** `RedactedBodyFields` matches field names by substring, so the default `token` entry also covers `__RequestVerificationToken`. It's stored as `***`, and a Razor Pages or MVC form post replayed as-is returns 400. Paste a fresh token from the page before sending.
- **Headers that weren't stored.** Anything in `ExcludedHeaders` (`Authorization`, `Cookie`, `X-Api-Key`, and the others) was dropped at capture time, so the replay goes out without it. Add the ones you need back in the headers box. Cookies can't be set there; they come from your browser, since the replay is a same-origin request.
- **Headers the browser sets itself.** `fetch()` can't send `Host`, `Content-Length`, `Origin`, `Referer`, `Accept-Encoding`, `Sec-*`, `Proxy-*`, and the rest of the [forbidden names](https://fetch.spec.whatwg.org/#forbidden-request-header). They're left out, and the panel lists them.
- **Bodies.** Multipart (file uploads) and other binary bodies can't be rebuilt from the captured text, so they aren't resent. Neither are bodies that weren't captured (`LogRequestBodies` off, or over `MaxBodySize`). GET and HEAD never send one.
- **Redirects.** The replay doesn't follow them. A 302 is the result, and the recorded entry shows it.
- **Another origin.** If the request was captured on a different host or port than the one the dashboard is open on, the replay still goes to the dashboard's origin. The panel warns about it.
- **`PathBase`.** Apps that run under a `PathBase` aren't supported, same as the rest of the dashboard.
- **"Not recorded".** After sending, the panel waits 5 seconds for the replayed request to be stored. A path in `ExcludedPaths`, a static file, or a route the dashboard doesn't see never shows up, so you get "Not recorded" along with the response status.

## The database file is corrupt or won't open

LiteDB writes a WAL file next to the database: `debug-dashboard-log.db`. Two rules:

- If you delete the database manually, **delete both files**. Deleting only `debug-dashboard.db` and leaving the `-log.db` behind corrupts the store on next startup ("page type must be collection page").
- Don't run two app instances against the same database path. Give each a distinct `DatabasePath`, or accept that the second instance fails to open the file.

When in doubt: stop the app, delete `debug-dashboard.db` *and* `debug-dashboard-log.db`, start again. It's dev data; it's disposable.

## Memory or disk usage grows too much

Cleanup runs every `CleanupInterval` (1 hour by default): it deletes entries older than `RetentionPeriod`, trims each entry type to `MaxEntries`, and if the database plus its `-log.db` file is still over `MaxDatabaseSize` (100MB), deletes the oldest entries and compacts the file. Before 2.4 the interval, retention period, and size cap were ignored, so an old database may shrink noticeably on the first run after upgrading. Details in [CONFIGURATION.md](CONFIGURATION.md#how-cleanup-works).

To keep less:

```csharp
builder.Services.AddDebugDashboard(config =>
{
    config.MaxEntries = 500;            // per entry type, oldest trimmed first
    config.LogRequestBodies = false;    // bodies are the heavy part
    config.LogResponseBodies = false;
    config.MaxBodySize = 64 * 1024;     // cap captured body size
    config.RetentionPeriod = TimeSpan.FromDays(1);
    config.MaxDatabaseSize = 20 * 1024 * 1024;
});
```

You can also trigger a trim manually (`POST /_debug/api/cleanup`) or wipe everything (`DELETE /_debug/api/clear`, or the Clear button in the dashboard).

## Noisy endpoints flood the capture

Exclude them:

```csharp
config.ExcludedPaths = new() { "/_debug", "/health", "/metrics", "/api/file-upload" };
```

Matching is contains-based on the path.

## DebugLogger or DebugDump entries land in the wrong app (integration tests)

`DebugLogger` and `DebugDump` are static, so they belong to whichever host called `UseDebugDashboard()` last. With several `WebApplicationFactory` hosts running in parallel in one test process, entries can end up in another host's store or get dropped. They never throw into your code. Run those test classes sequentially (an xUnit collection, or `DisableTestParallelization`) if you assert on them. The injected `IDebugLogger` doesn't have this problem.

## Version requirements

- .NET 8, 9, 10, or 11
- EF Core 8+ (matching your target framework) for query capture
- The 2.x dashboard requires no external resources. If the page loads blank, check the browser console and open an issue with what you see

## Still stuck

Open an issue: https://github.com/eladser/AspNetDebugDashboard/issues

Include the package version, .NET version, what you expected vs. what happened, and a minimal repro if you can. `GET /_debug/api/health` output is useful too; it shows the active configuration and storage state.
