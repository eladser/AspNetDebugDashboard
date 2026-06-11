# Contributing

Issues and PRs are welcome.

## Reporting bugs

Include the .NET version, the package version, and a minimal repro if you can. "Dashboard shows X when I expected Y" with a screenshot goes a long way.

## Building

You need the .NET 10 SDK and Node 20+.

```bash
# library + tests
dotnet build
dotnet test

# dashboard UI (only if you're touching dashboard/)
cd dashboard
npm install
npm run build   # writes src/AspNetDebugDashboard/wwwroot/index.html
```

The built `wwwroot/index.html` is committed, so a plain `dotnet build` works without Node. If you change anything under `dashboard/`, run `npm run build` and commit the regenerated file with your change.

To develop the UI against live data, run the sample app and the Vite dev server side by side:

```bash
dotnet run --project samples/SampleApp --urls http://localhost:5000
cd dashboard && npm run dev   # proxies /_debug/api to :5000
```

## Pull requests

- Branch off `main`, keep the change focused.
- Add or update tests for behavior changes. `dotnet test` must pass.
- Match the style of the file you're editing. No formatting-only churn.
- Update CHANGELOG.md under an Unreleased heading if the change is user-visible.

## Project layout

```
src/AspNetDebugDashboard/   the package: middleware, interceptor, storage, API controllers
dashboard/                  dashboard UI source (Vite + React + TS)
samples/SampleApp/          test bed with traffic-generating endpoints
tests/                      xUnit suite, runs on net8.0 and net10.0
docs/                       configuration and API reference
```
