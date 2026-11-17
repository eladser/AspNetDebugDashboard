using System.Diagnostics;
using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using AspNetDebugDashboard.Storage;
using FluentAssertions;
using LiteDB;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace AspNetDebugDashboard.Tests;

public class TimelineCaptureTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private FakeUpstream _upstream = null!;

    public Task InitializeAsync()
    {
        DebugContext.Current.Value = null;
        _upstream = new FakeUpstream();
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseContentRoot(Directory.GetCurrentDirectory());
            builder.ConfigureServices(s => s.PostConfigure<DebugConfiguration>(o => o.CaptureUnattachedOutgoingHttp = true));
        });
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        DebugDump.Detach();
        _factory.Dispose();
        _upstream.Dispose();
        return Task.CompletedTask;
    }

    private IDebugStorage Storage => _factory.Services.GetRequiredService<IDebugStorage>();

    private static async Task<T> WaitFor<T>(Func<Task<T?>> probe, int timeoutMs = 5000) where T : class
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            var v = await probe();
            if (v != null) return v;
            await Task.Delay(25);
        }
        throw new Xunit.Sdk.XunitException("not stored in time");
    }

    private async Task<RequestEntry> RunAsync(string pathAndQuery)
    {
        await _factory.CreateClient().GetAsync(pathAndQuery);
        var path = pathAndQuery.Split('?')[0];
        return await WaitFor(async () =>
        {
            var list = await Storage.GetRequestsAsync(new DebugFilter { PageSize = 50 });
            var hit = list.Items.FirstOrDefault(r => r.Path == path);
            return hit == null ? null : await Storage.GetRequestAsync(hit.Id);
        });
    }

    private static double End(double? offset, double? duration) => offset!.Value + duration!.Value;

    [Fact]
    public async Task OneRequest_SqlHttpLogDump_GetOffsetsInCallOrder()
    {
        var req = await RunAsync($"/test-timeline?url={Uri.EscapeDataString(_upstream.BaseUrl)}");

        req.DurationMs.Should().NotBeNull();
        var total = req.DurationMs!.Value;

        req.SqlQueries.Should().NotBeEmpty();
        req.OutgoingHttp.Should().ContainSingle();
        req.Logs.Should().ContainSingle();
        req.Dumps.Should().ContainSingle();

        foreach (var q in req.SqlQueries)
        {
            q.StartOffsetMs.Should().NotBeNull();
            q.DurationMs.Should().NotBeNull();
            End(q.StartOffsetMs, q.DurationMs).Should().BeLessThanOrEqualTo(total + 5);
        }
        var http = req.OutgoingHttp[0];
        http.StartOffsetMs.Should().NotBeNull();
        http.DurationMs.Should().NotBeNull();
        End(http.StartOffsetMs, http.DurationMs).Should().BeLessThanOrEqualTo(total + 5);

        var log = req.Logs[0].StartOffsetMs;
        var dump = req.Dumps[0].StartOffsetMs;
        log.Should().NotBeNull();
        dump.Should().NotBeNull();

        var sqlOffsets = req.SqlQueries.Select(q => q.StartOffsetMs!.Value).ToList();
        sqlOffsets.Should().BeInAscendingOrder();
        sqlOffsets.Last().Should().BeLessThan(http.StartOffsetMs!.Value);
        http.StartOffsetMs!.Value.Should().BeLessThan(log!.Value);
        log.Value.Should().BeLessThan(dump!.Value);
        dump.Value.Should().BeLessThanOrEqualTo(total + 5);

        req.StartOffsetMs.Should().BeNull();
    }

    [Fact]
    public async Task SyncEfPath_AttachesEveryQueryWithAnOffset()
    {
        var req = await RunAsync("/test-timeline-sync");

        // ensure-created, insert, two selects and a count at the very least
        req.SqlQueries.Count.Should().BeGreaterThanOrEqualTo(5);
        req.SqlQueries.Should().OnlyContain(q => q.StartOffsetMs != null && q.DurationMs != null);

        var stored = await WaitFor(async () =>
        {
            var all = await Storage.GetSqlQueriesAsync(new DebugFilter { RequestId = req.RequestId, PageSize = 100 });
            return all.Items.Count >= req.SqlQueries.Count ? all : null;
        });
        stored.Items.Select(q => q.Id).Should().BeEquivalentTo(req.SqlQueries.Select(q => q.Id));
    }

    [Fact]
    public async Task FailedSqlCommands_GetOffsets()
    {
        var req = await RunAsync("/test-timeline-fail");

        var failed = req.SqlQueries.Where(q => !q.IsSuccessful).ToList();
        failed.Should().HaveCount(2);
        failed.Should().OnlyContain(q => q.StartOffsetMs != null && q.DurationMs != null);
    }

    [Fact]
    public async Task FailedOutgoingCall_GetsAnOffset()
    {
        var dead = $"http://localhost:{FakeUpstream.GetFreePort()}/";
        var req = await RunAsync($"/test-outgoing?url={Uri.EscapeDataString(dead)}");

        var call = req.OutgoingHttp.Should().ContainSingle().Subject;
        call.Error.Should().NotBeNull();
        call.StartOffsetMs.Should().NotBeNull();
        call.DurationMs.Should().NotBeNull();
    }

    [Fact]
    public async Task ParallelOutgoingCalls_Overlap()
    {
        _upstream.Handler = _ => { Thread.Sleep(150); return (200, "ok"); };
        var req = await RunAsync($"/test-timeline-parallel?url={Uri.EscapeDataString(_upstream.BaseUrl)}");

        req.OutgoingHttp.Should().HaveCount(2);
        var (a, b) = (req.OutgoingHttp[0], req.OutgoingHttp[1]);
        b.StartOffsetMs.Should().BeLessThan(End(a.StartOffsetMs, a.DurationMs));
        a.StartOffsetMs.Should().BeLessThan(End(b.StartOffsetMs, b.DurationMs));
    }

    [Fact]
    public async Task OutgoingCallOutsideARequest_HasNoOffset()
    {
        _ = _factory.Services;
        DebugContext.Current.Value = null;
        using var client = new HttpClient();
        await client.GetAsync(_upstream.BaseUrl);

        var calls = await WaitFor(async () =>
        {
            var r = await Storage.GetOutgoingHttpAsync(new DebugFilter { PageSize = 10 });
            return r.Items.Count > 0 ? r : null;
        });
        calls.Items[0].StartOffsetMs.Should().BeNull();
        calls.Items[0].DurationMs.Should().NotBeNull();
    }

    [Fact]
    public async Task DumpAfterRequestCompleted_HasNoOffset()
    {
        var stored = new List<DumpEntry>();
        var storage = new Mock<IDebugStorage>();
        storage.Setup(s => s.StoreDumpAsync(It.IsAny<DumpEntry>()))
            .Callback<DumpEntry>(stored.Add).ReturnsAsync("id");
        var ctx = new DebugContext();
        DebugDump.Attach(storage.Object, ctx, new DebugConfiguration());

        var scope = new RequestScope("r1", null) { StartTimestamp = Stopwatch.GetTimestamp() };
        ctx.StartRequest("r1", new RequestEntry { RequestId = "r1" });
        DebugContext.Current.Value = scope;
        DebugDump.Dump(1, "during");
        ctx.CompleteRequest("r1");
        DebugDump.Dump(2, "after");
        DebugContext.Current.Value = null;
        await Task.Delay(50);

        stored.Single(d => d.Label == "during").StartOffsetMs.Should().NotBeNull();
        stored.Single(d => d.Label == "after").StartOffsetMs.Should().BeNull();
    }

    [Fact]
    public void OffsetMs_IsNullWithoutAScopeOrStartTimestamp()
    {
        var ctx = new DebugContext();
        ctx.StartRequest("r1", new RequestEntry());

        ctx.OffsetMs(null).Should().BeNull();
        ctx.OffsetMs(new RequestScope("r1", null)).Should().BeNull();
        ctx.OffsetMs(new RequestScope("unknown", null) { StartTimestamp = Stopwatch.GetTimestamp() }).Should().BeNull();
    }

    [Fact]
    public void OffsetMs_ForActiveRequest_IsRoundedToHundredths()
    {
        var ctx = new DebugContext();
        ctx.StartRequest("r1", new RequestEntry());
        var scope = new RequestScope("r1", null) { StartTimestamp = Stopwatch.GetTimestamp() };
        Thread.Sleep(5);

        var ms = ctx.OffsetMs(scope);
        ms.Should().BeGreaterThan(0);
        ms.Should().Be(Math.Round(ms!.Value, 2));

        ctx.CompleteRequest("r1");
        ctx.OffsetMs(scope).Should().BeNull();
    }

    [Fact]
    public async Task LiteDb_RoundTripKeepsTheDoubles()
    {
        var storage = new LiteDbStorage(":memory:", new DebugConfiguration { MaxEntries = 100 });
        var sql = new SqlQueryEntry { Query = "select 1", StartOffsetMs = 12.34, DurationMs = 0.57 };
        var http = new OutgoingHttpEntry { StartOffsetMs = 3.21, DurationMs = 99.99 };
        var req = new RequestEntry { DurationMs = 150.25, SqlQueries = { sql }, OutgoingHttp = { http } };

        var id = await storage.StoreRequestAsync(req);
        var back = (await storage.GetRequestAsync(id))!;

        back.DurationMs.Should().Be(150.25);
        back.StartOffsetMs.Should().BeNull();
        back.SqlQueries[0].StartOffsetMs.Should().Be(12.34);
        back.SqlQueries[0].DurationMs.Should().Be(0.57);
        back.OutgoingHttp[0].StartOffsetMs.Should().Be(3.21);
        back.OutgoingHttp[0].DurationMs.Should().Be(99.99);
    }

    [Fact]
    public void LiteDb_DocumentWithoutNewFields_DeserializesWithNulls()
    {
        using var db = new LiteDatabase(new MemoryStream());
        var raw = db.GetCollection("requests");
        raw.Insert(new BsonDocument
        {
            ["_id"] = "old",
            ["Method"] = "GET",
            ["ExecutionTimeMs"] = 42L,
            ["SqlQueries"] = new BsonArray { new BsonDocument { ["Query"] = "select 1", ["ExecutionTimeMs"] = 3L } },
        });

        var req = db.GetCollection<RequestEntry>("requests").FindById("old");

        req.ExecutionTimeMs.Should().Be(42);
        req.DurationMs.Should().BeNull();
        req.StartOffsetMs.Should().BeNull();
        req.SqlQueries.Should().ContainSingle();
        req.SqlQueries[0].StartOffsetMs.Should().BeNull();
        req.SqlQueries[0].DurationMs.Should().BeNull();
    }
}
