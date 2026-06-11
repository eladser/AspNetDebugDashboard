# Changelog

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/). Versions follow [SemVer](https://semver.org).

## [2.0.0] - 2026-06-11

The dashboard UI was rebuilt from scratch and the supported framework range was extended. Routes, API endpoints, and the public extension methods are unchanged, so upgrading from 1.x should not require code changes.

### Changed
- New dashboard UI: a Vite + React + TypeScript app compiled to a single self-contained HTML file and embedded in the assembly. The old page pulled React, Babel, Tailwind, and Font Awesome from CDNs at runtime and only showed counters; the new one has working tables for requests, queries, logs, and exceptions, detail panels with headers/bodies/stack traces, search, filtering, pagination, live refresh, and tab deep-linking (`/_debug#requests`).
- Multi-targets `net8.0`, `net9.0`, and `net10.0`. EF Core dependencies follow the target framework (8.x / 9.x / 10.x) so the package no longer forces an EF Core upgrade on net8 consumers.
- Test suite runs on both net8.0 and net10.0. Sample app moved to net10.0.
- README, docs, and package metadata rewritten to describe what the package actually does.

### Removed
- CDN dependencies (`cdn.tailwindcss.com`, unpkg React development builds, Babel standalone, Font Awesome). The dashboard now renders with zero external requests.
- Explicit `System.Text.Json` package reference; the framework reference provides it.

## [1.0.0] - 2025-01-07

First release. Request middleware, EF Core command interceptor, exception middleware, `IDebugLogger`, LiteDB storage, background cleanup, SignalR hub for realtime notifications, JSON export, and a dashboard page served at `/_debug`.
