# Changelog

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/). Versions follow [SemVer](https://semver.org).

## [2.4.0] - 2026-11-10

Existing setup is unchanged unless you implement `IDebugStorage` yourself (see Changed). Outgoing HTTP capture and live updates are on by default (still only in allowed environments). Outgoing request and response bodies stay off until you opt in.

### Added
- `net11.0` target, alongside `net8.0`, `net9.0`, and `net10.0`. EF Core 11 on that target. `AspNetDebugDashboard.Mcp` also targets `net11.0`.
- Outgoing HTTP capture. Calls made through any `HttpClient` (`new HttpClient()`, `IHttpClientFactory`, SDK clients built on it) are recorded through the runtime's `HttpHandlerDiagnosticListener`, with method, URL, status, timing, headers, and the inbound request they belong to. `ExcludedHeaders` applies to them, URL userinfo is stripped, and query parameters matching `RedactedBodyFields` are stored as `***`. Bodies are opt-in (`CaptureOutgoingHttpBodies`) and capped at `MaxOutgoingBodySize`. Retries show up as one entry per attempt. New Outgoing tab, an HTTP tab in request detail, `GET /api/http-requests` (and `/{id}`), and MCP `recent_outgoing_http`.
- `DebugDump.Dump(value, label)`: snapshots any object as a JSON tree at the call site, with file, line, and member. Cycles are marked, getter exceptions are stored as strings, and redacted field names are masked. Limited by `DumpMaxDepth`, `DumpMaxItems`, and `DumpMaxSize` (truncated dumps are flagged). Dumps taken during a request are attached to it, up to `MaxDumpsPerRequest`. A no-op unless the dashboard is active. New Dumps tab, a Dumps tab in request detail, `GET /api/dumps` (and `/{id}`), and MCP `recent_dumps`.
- Live updates over server-sent events at `GET /api/stream`. The dashboard refreshes when an entry is stored instead of polling every few seconds, throttled to one refresh per second with a trailing refresh so a burst's last rows still show up, and falls back to polling if the stream can't open or goes quiet.
- Options, with defaults: `CaptureOutgoingHttp` (true), `CaptureOutgoingHttpBodies` (false), `MaxOutgoingBodySize` (64 KB), `CaptureUnattachedOutgoingHttp` (false), `DumpMaxDepth` (6), `DumpMaxItems` (100), `DumpMaxSize` (64 KB), `MaxDumpsPerRequest` (50).
- Stats, search, export, clear, and bulk delete include dumps and outgoing calls.

### Changed
- `AspNetDebugDashboard.Mcp` runs on ModelContextProtocol 2.x (2.2.0). Microsoft.Extensions.Hosting and Microsoft.Extensions.Http bumped to 10.0.12 to match. Every tool now advertises an input schema, including parameterless ones.
- `EnableRealTimeUpdates` is no longer obsolete. It turns the event stream on or off (default on); with it off, `/api/stream` returns 404 and the UI polls.
- `IDebugStorage` has new members for outgoing calls, dumps, and compaction (`StoreOutgoingHttpAsync`, `StoreDumpAsync`, `CompactAsync`, `DeleteOldestFractionAsync`, and the matching getters). Custom storage implementations need to add them; the full list is in [docs/API.md](docs/API.md#idebugstorage-interface).
- `StorageCleanupService` is marked obsolete and no longer registered. `DebugDashboardCleanupService` is the only cleanup service.

### Fixed
- `RetentionPeriod` and `CleanupInterval` were ignored (cleanup always used 7 days and 1 hour). Both are honored now.
- `MaxDatabaseSize` was never enforced. When the database goes over it, cleanup deletes the oldest 25% of each collection and compacts the file, up to four passes, and logs a warning if it's still over. The health check uses the same limit instead of a hardcoded 100 MB.
- The database file is compacted after size cleanup, so deleted entries give disk space back.
- Database size now includes the `-log.db` file, and the log is checkpointed before measuring.
- The cleanup service no longer resolves storage outside `AllowedEnvironments`, which created the database file in Production.
- Cleanup errors are logged instead of swallowed, and a failed cycle doesn't stop the next one.
- `EnableRealTimeUpdates` set in `AddDebugDashboard` or appsettings was dropped by the options copy.
- Per-request capture lists are thread-safe, so parallel work inside one request (outgoing calls, dumps, logs, queries) no longer races.
- Capturing outgoing response bodies keeps the original `Content-Length`.
- Dumps never block on `Task.Result` and never enumerate an `IQueryable`, so dumping a pending task or an EF query doesn't hang or run SQL.
- `DebugLogger` (the static one) was never wired to a service provider, so `DebugLogger.InfoAsync`/`WarningAsync`/etc. did nothing. It's active whenever the dashboard is, and stays a no-op otherwise.
- A request whose handler threw was recorded with `StatusCode` 200. It's recorded as 500 now, unless the response already started with a different status.

## [2.3.0] - 2026-09-30

### Security
- The UI, API, and EF interceptor now check `IsEnabled` and the environment against a new `AllowedEnvironments` option (default: `Development` only) themselves, instead of relying only on `UseDebugDashboard()` being called correctly. Requests outside an allowed environment get a 404 even when `MapControllers()` is registered unconditionally.
- Added an optional `AuthorizationFilter` (`Func<HttpContext, bool>`) applied to every dashboard UI and API request; returns 401 when it rejects the request.
- Default `ExcludedHeaders` now also covers `Set-Cookie`, `X-Api-Key`, `X-Auth-Token`, and `Proxy-Authorization`.
- Added `RedactedBodyFields` (default: `password`, `secret`, `token`, `apikey`): matching field names in captured JSON and form-urlencoded bodies are replaced with `***` before storage.

### Fixed
- `EmitActivities = false` was silently ignored; the option is now carried over by `AddDebugDashboard`.
- A custom `BasePath` other than `/_debug` or `/_custom-debug` 404'd on every route; requests are now rewritten onto the canonical route internally so any `BasePath` works.

### Changed
- Removed the unused SignalR hub. `EnableRealTimeUpdates` and `AddDebugDashboardSignalR()` are marked obsolete and do nothing; the UI polls the API.
- Bumped EF Core to 8.0.31/9.0.20/10.0.12 (per-TFM), MimeKit to 4.18.1 (AspNetMailbox 0.1.1), ModelContextProtocol to 1.4.1, Microsoft.SourceLink.GitHub to 10.0.401, and vite to 8.3.1 across the dashboard and suite UIs.

## [2.2.0] - 2026-06-17

The dashboard becomes the hub of a tool suite. Existing setup is unchanged: `AddDebugDashboard`/`UseDebugDashboard`, routes, and options are the same, so upgrading from 2.1.x needs no code changes.

### Added
- A suite of sibling packages that share the dashboard's recipe (install, get a `/_*` page, no extra infrastructure) and a common navigation sidebar:
  - `AspNetMailbox` (0.1.0): captures outbound email in-process and previews it at `/_mailbox` (HTML/text/headers/raw/attachments, light or dark preview, `.eml` download). In-process SMTP sink on port 2525.
  - `AspNetFlags` (0.1.0): feature flags with a toggle UI at `/_flags`. Flags auto-discover when code checks them; add, delete, and filter from the page.
  - `AspNetJobs` (0.1.0): in-process background jobs with a live inspector at `/_jobs` (status, timing, stack traces, status filter, search, per-status counts).
  - `AspNetVitals` (0.1.0): live process vitals at `/_vitals` (memory sparkline, CPU, GC, threads, uptime, runtime detail) plus any registered health checks.
- Shared sidebar across the dashboard and every installed tool, so you can move between them. A tool installed on its own shows no sidebar. Backed by a small `AspNetDebugDashboard.Suite` (1.0.0) contract package.
- `AspNetDebugDashboard.Mcp` 2.2.0 adds `list_feature_flags`, `recent_jobs`, `app_vitals`, and `recent_mail` so an agent can read the rest of the suite.
- NuGet version and download badges for the `AspNetDebugDashboard.Mcp` package, in the main README and the package's own README.

### Changed
- Packages are now source-linked and deterministic, and ship `.snupkg` symbol packages, so you can step into the source from a stack trace.
- Accessibility: every page honors `prefers-reduced-motion`, has a favicon, and labels its icon-only controls.

## [2.1.1] - 2026-06-14

### Fixed
- README images (banner, demo gif, screenshots) didn't render on the NuGet listing because they used relative paths and raw HTML. They now use absolute URLs so the listing shows them.

## [2.1.0] - 2026-06-14

Two additive features. Existing setup is unchanged.

### Added
- OpenTelemetry tracing. Captured requests and queries are emitted as spans on the `AspNetDebugDashboard` ActivitySource. Add that source to your tracer (`AddSource("AspNetDebugDashboard")`) and they flow to Aspire or any OTLP backend, carrying the request id, status, query text, and timing. Off when nothing is listening, so it costs nothing until you wire it up. Controlled by the `EmitActivities` option (default on).
- `AspNetDebugDashboard.Mcp`, a separate dotnet tool that runs an MCP server over the dashboard's REST API. It gives a coding agent eight read-only tools (recent requests, a request's SQL and logs, recent queries/exceptions/logs, performance, search) against your running app. See its [README](src/AspNetDebugDashboard.Mcp/README.md).

## [2.0.0] - 2026-06-11

The dashboard UI was rebuilt from scratch and the supported framework range was extended. Routes, API endpoints, and the public extension methods are unchanged, so upgrading from 1.x should not require code changes.

### Added
- Performance page backed by the existing `/api/performance` endpoint: req/min, avg, median, P95, P99, error rate, slowest endpoints, status distribution over the last hour.
- Global search (`Ctrl+K`) across requests, queries, logs, and exceptions.
- Request detail panel with tabs (Summary, Headers, Request, Response, SQL, Logs), a copy-as-cURL button, and an N+1 warning when the same query runs three or more times in one request.
- SQL syntax highlighting and copy buttons on every code block.
- Cross-links from a query, log, or exception to the request that produced it.
- Column sorting, "Failed only" / "Slow only" filter chips, and keyboard navigation (`j`/`k` rows, `Enter` open, `/` filter, `Esc` close).
- Relative timestamps with the exact time on hover.
- Package icon and repository branding (logo, README banner, social preview image, demo GIF).

### Changed
- New dashboard UI: a Vite + React + TypeScript app compiled to a single self-contained HTML file and embedded in the assembly. The old page pulled React, Babel, Tailwind, and Font Awesome from CDNs at runtime and only showed counters.
- Multi-targets `net8.0`, `net9.0`, and `net10.0`. EF Core dependencies follow the target framework (8.x / 9.x / 10.x) so the package no longer forces an EF Core upgrade on net8 consumers.
- Test suite runs on both net8.0 and net10.0. Sample app moved to net10.0 and SQLite so query capture works out of the box.
- README, docs, and package metadata rewritten to describe what the package actually does.

### Fixed
- The EF Core interceptor appended a tracking comment to `DbCommand.CommandText` and rewrote it after execution, which corrupted the SQL sent to the database and crashed SQLite ("an open reader is associated with this command"). It now reads `eventData.Duration` and never touches the command.
- Requests stored empty `SqlQueries` and `Logs` lists: the middleware never registered requests with `DebugContext`, so nothing the interceptor or `IDebugLogger` captured was ever attached. Request entries also now carry `Url`, `QueryString`, `Protocol`, and `IsHttps`.
- The requests list ignored the `search` parameter; `isSuccessful`, `isSlowQuery`, `minExecutionTime`, and `requestId` filters were declared but never applied by the storage layer.

### Removed
- CDN dependencies (`cdn.tailwindcss.com`, unpkg React development builds, Babel standalone, Font Awesome). The dashboard now renders with zero external requests.
- Explicit `System.Text.Json` package reference; the framework reference provides it.

## [1.0.0] - 2025-01-07

First release. Request middleware, EF Core command interceptor, exception middleware, `IDebugLogger`, LiteDB storage, background cleanup, SignalR hub for realtime notifications, JSON export, and a dashboard page served at `/_debug`.
