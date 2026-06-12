# Security Policy

## Supported versions

| Version | Supported |
| ------- | --------- |
| 2.0.x   | yes       |
| 1.0.x   | no        |

## Reporting a vulnerability

Don't open a public issue. Use [GitHub's private vulnerability reporting](https://github.com/eladser/AspNetDebugDashboard/security/advisories/new) on this repository and include what you found, how to reproduce it, and what the impact is. You'll get a response within a few days, and credit in the advisory unless you'd rather stay anonymous.

## What this package does and doesn't protect

This is a development tool. The dashboard has **no authentication** and captures whatever flows through your app: request bodies, headers, SQL with parameter values, stack traces. The protections that exist:

- `UseDebugDashboard()` is a no-op outside the Development environment. Enabling it elsewhere requires an explicit `forceEnable: true`.
- `Authorization` and `Cookie` headers are excluded from capture by default (`ExcludedHeaders`).
- Response body capture is off by default (`LogResponseBodies = false`).
- Body capture is size-capped (`MaxBodySize`, default 1 MB).

What's on you:

- If you force-enable it anywhere reachable by other people, put your own auth in front of the `/_debug` path.
- Extend `ExcludedHeaders` / `ExcludedPaths` for anything sensitive your app handles (API keys, auth endpoints, payment routes).
- The LiteDB file (`debug-dashboard.db`) contains everything captured, in plain form. Don't commit it, don't ship it. It's covered by the repo's `.gitignore` pattern, but check your own.
