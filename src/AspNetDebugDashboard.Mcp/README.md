# AspNetDebugDashboard.Mcp

An MCP server that lets a coding agent (Claude, Copilot, etc.) read the data [AspNetDebugDashboard](https://github.com/eladser/AspNetDebugDashboard) captures from your running app: recent requests, the SQL a request ran, recent failures, slow queries, performance numbers.

It's a thin client over the dashboard's REST API, so your app needs to be running with the dashboard enabled.

## Install

```bash
dotnet tool install --global AspNetDebugDashboard.Mcp
```

## Configure your agent

Point your MCP client at the `aspnet-debug-mcp` command. For Claude Desktop / Claude Code, add it to your MCP config:

```json
{
  "mcpServers": {
    "debug-dashboard": {
      "command": "aspnet-debug-mcp",
      "env": { "DEBUG_DASHBOARD_URL": "http://localhost:5000" }
    }
  }
}
```

`DEBUG_DASHBOARD_URL` is where your app is running (default `http://localhost:5000`). You can also pass it as the first argument instead.

## Tools

- `get_stats`: counts, averages, distributions, slowest requests and queries
- `recent_requests`: newest requests, optionally failed-only
- `get_request`: one request in full, with its SQL and logs
- `recent_queries`: newest SQL, optionally slow-only
- `recent_exceptions`: newest unhandled exceptions with stack traces
- `recent_logs`: newest logs, optionally by level
- `performance`: last-hour metrics (P95/P99, error rate, slowest endpoints)
- `search`: across all captured data

## License

MIT
