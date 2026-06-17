# Changelog

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/). Versions follow [SemVer](https://semver.org).

## [Unreleased]

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
