using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using FluentAssertions;
using Xunit;

namespace AspNetDebugDashboard.Tests;

public class DebugContextTests
{
    [Fact]
    public async Task Parallel_AddOutgoingHttp_calls_all_land()
    {
        var ctx = new DebugContext();
        var requestId = Guid.NewGuid().ToString();
        ctx.StartRequest(requestId, new RequestEntry { RequestId = requestId });

        var tasks = Enumerable.Range(0, 50)
            .Select(i => Task.Run(() => ctx.AddOutgoingHttp(requestId, new OutgoingHttpEntry { RequestId = requestId })));
        await Task.WhenAll(tasks);

        var request = ctx.CompleteRequest(requestId);
        request!.OutgoingHttp.Should().HaveCount(50);
    }

    [Fact]
    public async Task Parallel_AddSqlQuery_and_AddLog_calls_all_land()
    {
        var ctx = new DebugContext();
        var requestId = Guid.NewGuid().ToString();
        ctx.StartRequest(requestId, new RequestEntry { RequestId = requestId });

        var sqlTasks = Enumerable.Range(0, 50)
            .Select(i => Task.Run(() => ctx.AddSqlQuery(requestId, new SqlQueryEntry { RequestId = requestId })));
        var logTasks = Enumerable.Range(0, 50)
            .Select(i => Task.Run(() => ctx.AddLog(requestId, new LogEntry { RequestId = requestId })));
        await Task.WhenAll(sqlTasks.Concat(logTasks));

        var request = ctx.CompleteRequest(requestId);
        request!.SqlQueries.Should().HaveCount(50);
        request.Logs.Should().HaveCount(50);
    }
}
