using System.Diagnostics;
using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using FluentAssertions;
using Xunit;

namespace AspNetDebugDashboard.Tests;

public class DebugTelemetryTests
{
    // Collect spans the same way OpenTelemetry would: a listener on the source.
    private static List<Activity> Capture(Action act)
    {
        var spans = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == DebugTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = spans.Add,
        };
        ActivitySource.AddActivityListener(listener);
        act();
        return spans;
    }

    [Fact]
    public void RecordRequest_emits_server_span_with_tags()
    {
        var spans = Capture(() => DebugTelemetry.RecordRequest(new RequestEntry
        {
            Method = "GET", Path = "/api/orders", QueryString = "?page=2", StatusCode = 200,
            ExecutionTimeMs = 85, RequestId = "req-1", Timestamp = DateTime.UtcNow,
            SqlQueries = new() { new SqlQueryEntry(), new SqlQueryEntry() },
        }));

        var span = spans.Should().ContainSingle().Subject;
        span.DisplayName.Should().Be("GET /api/orders");
        span.Kind.Should().Be(ActivityKind.Server);
        span.GetTagItem("http.request.method").Should().Be("GET");
        span.GetTagItem("url.path").Should().Be("/api/orders");
        span.GetTagItem("http.response.status_code").Should().Be(200);
        span.GetTagItem("debug.request_id").Should().Be("req-1");
        span.GetTagItem("debug.sql_query_count").Should().Be(2);
        span.Duration.TotalMilliseconds.Should().BeApproximately(85, 5);
        span.Status.Should().Be(ActivityStatusCode.Unset);
    }

    [Fact]
    public void RecordRequest_marks_5xx_as_error()
    {
        var spans = Capture(() => DebugTelemetry.RecordRequest(new RequestEntry
        {
            Method = "POST", Path = "/api/pay", StatusCode = 500, ExecutionTimeMs = 10, Timestamp = DateTime.UtcNow,
        }));

        spans.Should().ContainSingle().Which.Status.Should().Be(ActivityStatusCode.Error);
    }

    [Fact]
    public void RecordQuery_emits_client_span_with_sql()
    {
        var spans = Capture(() => DebugTelemetry.RecordQuery(new SqlQueryEntry
        {
            Query = "SELECT * FROM Orders WHERE Id = @p0", Database = "shop",
            ExecutionTimeMs = 1200, IsSlowQuery = true, RowsAffected = 1,
            RequestId = "req-1", Timestamp = DateTime.UtcNow, IsSuccessful = true,
        }));

        var span = spans.Should().ContainSingle().Subject;
        span.DisplayName.Should().Be("db.query");
        span.Kind.Should().Be(ActivityKind.Client);
        span.GetTagItem("db.query.text").Should().Be("SELECT * FROM Orders WHERE Id = @p0");
        span.GetTagItem("db.namespace").Should().Be("shop");
        span.GetTagItem("debug.slow_query").Should().Be(true);
    }

    [Fact]
    public void Span_start_never_after_end_even_with_bad_duration()
    {
        // A negative/garbage duration must not produce an inverted span (exporters drop those).
        var spans = Capture(() => DebugTelemetry.RecordRequest(new RequestEntry
        {
            Method = "GET", Path = "/x", StatusCode = 200, ExecutionTimeMs = -5, Timestamp = DateTime.UtcNow,
        }));

        spans.Should().ContainSingle().Which.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void No_listener_means_no_work()
    {
        // Without a registered listener the source produces nothing, so this is a no-op (and must not throw).
        var act = () => DebugTelemetry.RecordRequest(new RequestEntry { Method = "GET", Path = "/x", Timestamp = DateTime.UtcNow });
        act.Should().NotThrow();
    }
}
