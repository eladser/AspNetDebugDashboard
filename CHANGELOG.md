# Changelog

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/). Versions follow [SemVer](https://semver.org).

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
