using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SampleApp.Data;

// SQLite runs in-process, so queries take microseconds. The delay makes the sample behave
// like a database server over the network, so the N+1 shows up the way it would in a real app.
// It waits on a stopwatch because Task.Delay and Thread.Sleep round up to Windows' 15.6 ms tick.
public class LatencyInterceptor : DbCommandInterceptor
{
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(4);

    private static void Wait()
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < Delay) Thread.SpinWait(50);
    }

    private static async Task WaitAsync()
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < Delay) await Task.Yield();
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Wait();
        return result;
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        await WaitAsync();
        return result;
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Wait();
        return result;
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await WaitAsync();
        return result;
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Wait();
        return result;
    }

    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        await WaitAsync();
        return result;
    }
}
