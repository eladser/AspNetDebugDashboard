using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace AspNetDebugDashboard.Tests;

public class DebugDumpTests : IDisposable
{
    public DebugDumpTests() => DebugDump.Detach();
    public void Dispose() => DebugDump.Detach();

    [Fact]
    public void Dump_before_attach_is_a_silent_no_op()
    {
        var act = () => DebugDump.Dump(new { x = 1 }, "test");
        act.Should().NotThrow();
    }

    [Fact]
    public void Dump_while_inactive_allocates_nothing()
    {
        var value = new object();

        // warm up the JIT before measuring
        DebugDump.Dump(value);
        DebugDump.Dump(value);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            DebugDump.Dump(value);
        }
        var after = GC.GetAllocatedBytesForCurrentThread();

        (after - before).Should().Be(0);
    }

    [Fact]
    public async Task Dump_attaches_to_the_current_request_and_stores()
    {
        var storage = new Mock<IDebugStorage>();
        storage.Setup(s => s.StoreDumpAsync(It.IsAny<DumpEntry>())).ReturnsAsync("id");
        var context = new DebugContext();
        var config = new DebugConfiguration();

        DebugDump.Attach(storage.Object, context, config);

        const string requestId = "req-42";
        context.StartRequest(requestId, new RequestEntry { RequestId = requestId });
        DebugContext.Current.Value = new RequestScope(requestId, null);

        DebugDump.Dump(new { order = 7 }, "created order");

        DebugContext.Current.Value = null;

        // storage is fire-and-forget; give the continuation a beat to run
        await Task.Delay(50);

        var completed = context.CompleteRequest(requestId);
        completed.Should().NotBeNull();
        completed!.Dumps.Should().ContainSingle();
        completed.Dumps[0].Label.Should().Be("created order");
        completed.Dumps[0].RequestId.Should().Be(requestId);

        storage.Verify(s => s.StoreDumpAsync(It.Is<DumpEntry>(d => d.RequestId == requestId)), Times.Once);
    }

    [Fact]
    public void Dump_outside_a_request_still_stores_with_no_request_id()
    {
        var storage = new Mock<IDebugStorage>();
        storage.Setup(s => s.StoreDumpAsync(It.IsAny<DumpEntry>())).ReturnsAsync("id");
        var context = new DebugContext();
        var config = new DebugConfiguration();

        DebugDump.Attach(storage.Object, context, config);
        DebugContext.Current.Value = null;

        DebugDump.Dump("background job result");

        storage.Verify(s => s.StoreDumpAsync(It.Is<DumpEntry>(d => d.RequestId == "")), Times.Once);
    }

    [Fact]
    public void MaxDumpsPerRequest_caps_how_many_attach_to_one_request()
    {
        var storage = new Mock<IDebugStorage>();
        storage.Setup(s => s.StoreDumpAsync(It.IsAny<DumpEntry>())).ReturnsAsync("id");
        var context = new DebugContext();
        var config = new DebugConfiguration { MaxDumpsPerRequest = 2 };

        DebugDump.Attach(storage.Object, context, config);

        const string requestId = "req-cap";
        context.StartRequest(requestId, new RequestEntry { RequestId = requestId });
        DebugContext.Current.Value = new RequestScope(requestId, null);

        for (var i = 0; i < 5; i++)
        {
            DebugDump.Dump(i, $"dump-{i}");
        }

        DebugContext.Current.Value = null;
        var completed = context.CompleteRequest(requestId);

        completed!.Dumps.Should().HaveCount(2);
    }
}
