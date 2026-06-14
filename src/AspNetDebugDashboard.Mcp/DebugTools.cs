using System.ComponentModel;
using ModelContextProtocol.Server;

namespace AspNetDebugDashboard.Mcp;

// Tools an agent can call while debugging a running app. Each returns the raw
// JSON from the dashboard's REST API.
[McpServerToolType]
public static class DebugTools
{
    [McpServerTool(Name = "get_stats")]
    [Description("Totals and distributions: request/query/log/exception counts, average response and query times, status-code and method breakdowns, slowest requests and queries.")]
    public static Task<string> GetStats(DashboardClient client, CancellationToken ct)
        => client.GetAsync("/stats", ct);

    [McpServerTool(Name = "recent_requests")]
    [Description("Most recent HTTP requests, newest first. Set failedOnly to see only 4xx/5xx responses.")]
    public static Task<string> RecentRequests(
        DashboardClient client,
        [Description("How many to return (default 20).")] int count = 20,
        [Description("Only return requests that failed (status >= 400).")] bool failedOnly = false,
        CancellationToken ct = default)
    {
        var q = $"/requests?page=1&pageSize={count}&sortBy=timestamp&sortDescending=true";
        if (failedOnly) q += "&isSuccessful=false";
        return client.GetAsync(q, ct);
    }

    [McpServerTool(Name = "get_request")]
    [Description("Full detail for one request by id, including its captured body, headers, the SQL queries it ran, and the logs it wrote.")]
    public static Task<string> GetRequest(
        DashboardClient client,
        [Description("The request id.")] string id,
        CancellationToken ct = default)
        => client.GetAsync($"/requests/{Uri.EscapeDataString(id)}", ct);

    [McpServerTool(Name = "recent_queries")]
    [Description("Most recent SQL queries with text, parameters, and timing. Set slowOnly to see only queries flagged slow.")]
    public static Task<string> RecentQueries(
        DashboardClient client,
        [Description("How many to return (default 20).")] int count = 20,
        [Description("Only return slow queries.")] bool slowOnly = false,
        CancellationToken ct = default)
    {
        var q = $"/queries?page=1&pageSize={count}&sortBy=timestamp&sortDescending=true";
        if (slowOnly) q += "&isSlowQuery=true";
        return client.GetAsync(q, ct);
    }

    [McpServerTool(Name = "recent_exceptions")]
    [Description("Most recent unhandled exceptions with type, message, stack trace, and the route that threw.")]
    public static Task<string> RecentExceptions(
        DashboardClient client,
        [Description("How many to return (default 20).")] int count = 20,
        CancellationToken ct = default)
        => client.GetAsync($"/exceptions?page=1&pageSize={count}&sortBy=timestamp&sortDescending=true", ct);

    [McpServerTool(Name = "recent_logs")]
    [Description("Most recent log entries. Optionally filter by level (Info, Warning, Error, Critical, Debug, Trace).")]
    public static Task<string> RecentLogs(
        DashboardClient client,
        [Description("How many to return (default 20).")] int count = 20,
        [Description("Log level to filter by, or null for all.")] string? level = null,
        CancellationToken ct = default)
    {
        var q = $"/logs?page=1&pageSize={count}&sortBy=timestamp&sortDescending=true";
        if (!string.IsNullOrWhiteSpace(level)) q += $"&level={Uri.EscapeDataString(level)}";
        return client.GetAsync(q, ct);
    }

    [McpServerTool(Name = "performance")]
    [Description("Performance over the last hour: request rate, average/median/P95/P99 response times, error rate, and slowest endpoints.")]
    public static Task<string> Performance(DashboardClient client, CancellationToken ct)
        => client.GetAsync("/performance", ct);

    [McpServerTool(Name = "search")]
    [Description("Search across requests, queries, logs, and exceptions for a term.")]
    public static Task<string> Search(
        DashboardClient client,
        [Description("The text to search for.")] string term,
        CancellationToken ct = default)
        => client.GetAsync($"/search?term={Uri.EscapeDataString(term)}", ct);
}
