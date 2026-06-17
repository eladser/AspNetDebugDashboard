using AspNetJobs;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AspNetDebugDashboard.Tests;

public class JobsTests : IDisposable
{
    private readonly string _db = Path.Combine(Path.GetTempPath(), $"jobs-test-{Guid.NewGuid():N}.db");

    private LiteDbJobStore Store() => new(new JobsOptions { DatabasePath = _db });

    [Fact]
    public void Store_save_get_list_and_clear_finished()
    {
        using var store = Store();
        store.Save(new JobRecord { JobId = "a", Name = "one", Status = JobStatus.Succeeded });
        store.Save(new JobRecord { JobId = "b", Name = "two", Status = JobStatus.Running });

        store.Get("a")!.Name.Should().Be("one");
        store.List(50).Should().HaveCount(2);

        store.Clear(); // drops finished, keeps in-flight
        store.List(50).Should().ContainSingle(j => j.JobId == "b");
    }

    [Fact]
    public async Task Queue_runs_jobs_recording_success_and_failure()
    {
        using var store = Store();
        var queue = new JobQueue(store, NullLogger<JobQueue>.Instance);
        await queue.StartAsync(CancellationToken.None);
        try
        {
            var okId = queue.Enqueue("ok", async _ => await Task.Yield());
            var badId = queue.Enqueue("bad", async _ => { await Task.Yield(); throw new InvalidOperationException("boom"); });

            var ok = await WaitForTerminal(store, okId);
            var bad = await WaitForTerminal(store, badId);

            ok.Status.Should().Be(JobStatus.Succeeded);
            ok.DurationMs.Should().NotBeNull();
            bad.Status.Should().Be(JobStatus.Failed);
            bad.Error.Should().Contain("boom");
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }

    private static async Task<JobRecord> WaitForTerminal(LiteDbJobStore store, string jobId)
    {
        for (var i = 0; i < 100; i++)
        {
            var rec = store.Get(jobId);
            if (rec is { Status: JobStatus.Succeeded or JobStatus.Failed }) return rec;
            await Task.Delay(50);
        }
        throw new TimeoutException($"job {jobId} did not finish");
    }

    public void Dispose()
    {
        if (File.Exists(_db)) File.Delete(_db);
    }
}
