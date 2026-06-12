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

3. Controllers are mapped — the dashboard and its API are controllers, so the app needs `app.MapControllers()` (and `AddDebugDashboard` must run before `Build()`).

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

   Note the `(sp, options)` overload — the interceptor needs the service provider.

2. **`UseInMemoryDatabase` produces no SQL.** The EF in-memory provider doesn't go through the relational command pipeline, so there's nothing to intercept. Use SQLite (`UseSqlite("Data Source=dev.db")`) if you want captured queries during local development.

3. `LogSqlQueries` must be true (it is by default).

4. Raw ADO.NET (`SqlCommand` used directly) bypasses EF and isn't captured. Only commands that flow through EF Core's interceptor pipeline are.

## Logs don't appear

Only entries written through this package's logger are captured — `IDebugLogger` (injected) or the static `DebugLogger`. Output from `ILogger<T>` / Serilog / NLog is **not** picked up; that's a different pipeline.

## Queries/logs aren't attached to their request

Entries are correlated by `HttpContext.TraceIdentifier` while the request is in flight. Work done outside a request (background services, startup seeding, hosted jobs) is still captured, but lands unattached — you'll see it in the Queries/Logs tabs with no parent request link.

## The database file is corrupt or won't open

LiteDB writes a WAL file next to the database: `debug-dashboard-log.db`. Two rules:

- If you delete the database manually, **delete both files**. Deleting only `debug-dashboard.db` and leaving the `-log.db` behind corrupts the store on next startup ("page type must be collection page").
- Don't run two app instances against the same database path. Give each a distinct `DatabasePath`, or accept that the second instance fails to open the file.

When in doubt: stop the app, delete `debug-dashboard.db` *and* `debug-dashboard-log.db`, start again. It's dev data; it's disposable.

## Memory or disk usage grows too much

```csharp
builder.Services.AddDebugDashboard(config =>
{
    config.MaxEntries = 500;            // per entry type, oldest trimmed first
    config.LogRequestBodies = false;    // bodies are the heavy part
    config.LogResponseBodies = false;
    config.MaxBodySize = 64 * 1024;     // cap captured body size
    config.RetentionPeriod = TimeSpan.FromDays(1);
});
```

You can also trigger a trim manually (`POST /_debug/api/cleanup`) or wipe everything (`DELETE /_debug/api/clear`, or the Clear button in the dashboard).

## Noisy endpoints flood the capture

Exclude them:

```csharp
config.ExcludedPaths = new() { "/_debug", "/health", "/metrics", "/api/file-upload" };
```

Matching is contains-based on the path.

## Version requirements

- .NET 8, 9, or 10
- EF Core 8+ (matching your target framework) for query capture
- The 2.x dashboard requires no external resources — if the page loads blank, check the browser console and open an issue with what you see

## Still stuck

Open an issue: https://github.com/eladser/AspNetDebugDashboard/issues

Include the package version, .NET version, what you expected vs. what happened, and a minimal repro if you can. `GET /_debug/api/health` output is useful too — it shows the active configuration and storage state.
