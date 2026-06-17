using System.ComponentModel;
using ModelContextProtocol.Server;

namespace AspNetDebugDashboard.Mcp;

// Tools for the rest of the AspNet* suite (Mailbox, Flags, Jobs, Vitals).
// Each hits the tool's own /_route/api and returns raw JSON. If a tool isn't
// installed the call returns a small error object rather than throwing.
[McpServerToolType]
public static class SuiteTools
{
    [McpServerTool(Name = "list_feature_flags")]
    [Description("All AspNetFlags feature flags with their on/off state, from /_flags. Use this to see which features are enabled in the running app.")]
    public static Task<string> ListFeatureFlags(DashboardClient client, CancellationToken ct)
        => client.GetSuiteAsync("/_flags/api/flags", ct);

    [McpServerTool(Name = "recent_jobs")]
    [Description("Recent AspNetJobs background jobs (newest first) with status, timing, and any failure stack trace, from /_jobs.")]
    public static Task<string> RecentJobs(
        DashboardClient client,
        [Description("How many to return (default 50).")] int count = 50,
        CancellationToken ct = default)
        => client.GetSuiteAsync($"/_jobs/api/jobs?limit={count}", ct);

    [McpServerTool(Name = "app_vitals")]
    [Description("Current AspNetVitals snapshot: memory, GC, CPU, threads, uptime, runtime, and the result of every registered health check, from /_vitals.")]
    public static Task<string> AppVitals(DashboardClient client, CancellationToken ct)
        => client.GetSuiteAsync("/_vitals/api/vitals", ct);

    [McpServerTool(Name = "recent_mail")]
    [Description("Recently captured outbound email from AspNetMailbox (subject, from, to, attachment count), from /_mailbox.")]
    public static Task<string> RecentMail(
        DashboardClient client,
        [Description("How many to return (default 20).")] int count = 20,
        CancellationToken ct = default)
        => client.GetSuiteAsync($"/_mailbox/api/messages?page=1&pageSize={count}", ct);
}
