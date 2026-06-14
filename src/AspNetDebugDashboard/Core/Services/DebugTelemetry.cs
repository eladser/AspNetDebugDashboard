using System.Diagnostics;
using AspNetDebugDashboard.Core.Models;

namespace AspNetDebugDashboard.Core.Services;

// Emits captured requests and queries as Activity spans so they can flow into
// OpenTelemetry. Add the source to your tracer to receive them:
//
//     .WithTracing(t => t.AddSource(DebugTelemetry.SourceName).AddOtlpExporter())
//
// StartActivity returns null when nothing is listening, so this costs nothing
// until someone opts in by adding the source.
public static class DebugTelemetry
{
    public const string SourceName = "AspNetDebugDashboard";

    private static readonly ActivitySource Source = new(SourceName);

    public static void RecordRequest(RequestEntry r)
    {
        // CreateActivity leaves the span unstarted so we can back-date it to the
        // request we already timed. Returns null when nothing is listening.
        using var act = Source.CreateActivity($"{r.Method} {r.Path}", ActivityKind.Server);
        if (act is null) return;

        var end = r.Timestamp;
        act.SetStartTime(StartOf(end, r.ExecutionTimeMs));
        act.Start();

        act.SetTag("http.request.method", r.Method);
        act.SetTag("url.path", r.Path);
        if (!string.IsNullOrEmpty(r.QueryString)) act.SetTag("url.query", r.QueryString);
        act.SetTag("http.response.status_code", r.StatusCode);
        act.SetTag("debug.request_id", r.RequestId);
        act.SetTag("debug.sql_query_count", r.SqlQueries?.Count ?? 0);
        if (r.StatusCode >= 500) act.SetStatus(ActivityStatusCode.Error);

        act.SetEndTime(end);
        act.Stop();
    }

    public static void RecordQuery(SqlQueryEntry q)
    {
        using var act = Source.CreateActivity("db.query", ActivityKind.Client);
        if (act is null) return;

        var end = q.Timestamp;
        act.SetStartTime(StartOf(end, q.ExecutionTimeMs));
        act.Start();

        act.SetTag("db.system", "sql");
        act.SetTag("db.query.text", q.Query);
        if (!string.IsNullOrEmpty(q.Database)) act.SetTag("db.namespace", q.Database);
        act.SetTag("debug.request_id", q.RequestId);
        act.SetTag("debug.rows_affected", q.RowsAffected);
        act.SetTag("debug.slow_query", q.IsSlowQuery);
        if (!q.IsSuccessful) act.SetStatus(ActivityStatusCode.Error, q.Error);

        act.SetEndTime(end);
        act.Stop();
    }

    // Never let a stale timestamp or odd duration produce an inverted span;
    // exporters drop those.
    private static DateTime StartOf(DateTime end, long durationMs)
    {
        var start = end - TimeSpan.FromMilliseconds(Math.Max(0, durationMs));
        return start <= end ? start : end;
    }
}
