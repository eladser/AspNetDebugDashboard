using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AspNetJobs;

internal sealed record QueuedWork(string JobId, string Name, Func<CancellationToken, Task> Work);

internal sealed class JobQueue : BackgroundService, IJobQueue
{
    private readonly Channel<QueuedWork> _ch = Channel.CreateUnbounded<QueuedWork>();
    private readonly IJobStore _store;
    private readonly ILogger<JobQueue> _log;

    public JobQueue(IJobStore store, ILogger<JobQueue> log)
    {
        _store = store;
        _log = log;
    }

    public string Enqueue(string name, Func<CancellationToken, Task> work)
    {
        var id = Guid.NewGuid().ToString("n");
        _store.Save(new JobRecord { JobId = id, Name = name, Status = JobStatus.Pending });
        // unbounded channel: write never blocks, never drops
        _ch.Writer.TryWrite(new QueuedWork(id, name, work));
        return id;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _ch.Reader.ReadAllAsync(stoppingToken))
            await Run(job, stoppingToken);
    }

    private async Task Run(QueuedWork job, CancellationToken ct)
    {
        var rec = _store.Get(job.JobId) ?? new JobRecord { JobId = job.JobId, Name = job.Name };
        rec.Status = JobStatus.Running;
        rec.StartedAt = DateTime.UtcNow;
        _store.Save(rec);

        var sw = Stopwatch.StartNew();
        try
        {
            await job.Work(ct);
            rec.Status = JobStatus.Succeeded;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // app is shutting down, not a real failure
            rec.Status = JobStatus.Failed;
            rec.Error = "Cancelled on shutdown.";
        }
        catch (Exception ex)
        {
            rec.Status = JobStatus.Failed;
            var s = ex.ToString();
            rec.Error = s.Length > 8192 ? s[..8192] + "\n…(truncated)" : s;
            _log.LogError(ex, "Job {Name} ({JobId}) failed", job.Name, job.JobId);
        }
        sw.Stop();
        rec.FinishedAt = DateTime.UtcNow;
        rec.DurationMs = sw.Elapsed.TotalMilliseconds;
        _store.Save(rec);
    }
}
