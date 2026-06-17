using LiteDB;

namespace AspNetJobs;

internal interface IJobStore
{
    void Save(JobRecord job);
    JobRecord? Get(string jobId);
    IReadOnlyList<JobRecord> List(int limit);
    void Clear();
}

internal sealed class LiteDbJobStore : IJobStore, IDisposable
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<JobRecord> _col;
    private readonly int _max;
    private readonly object _gate = new();

    public LiteDbJobStore(JobsOptions options)
    {
        _db = new LiteDatabase(options.DatabasePath);
        _col = _db.GetCollection<JobRecord>("jobs");
        _col.EnsureIndex(x => x.JobId, unique: true);
        _col.EnsureIndex(x => x.EnqueuedAt);
        _max = options.MaxRecords;
    }

    public void Save(JobRecord job)
    {
        lock (_gate)
        {
            var existing = _col.FindOne(x => x.JobId == job.JobId);
            if (existing != null) job.Id = existing.Id;
            _col.Upsert(job);

            // trim oldest finished records beyond the cap; never touch in-flight jobs
            var count = _col.Count();
            if (count > _max)
            {
                var stale = _col.Query()
                    .Where(x => x.Status == JobStatus.Succeeded || x.Status == JobStatus.Failed)
                    .OrderBy(x => x.EnqueuedAt)
                    .Limit(count - _max)
                    .ToList();
                foreach (var s in stale) _col.Delete(s.Id);
            }
        }
    }

    public JobRecord? Get(string jobId)
    {
        lock (_gate) return _col.FindOne(x => x.JobId == jobId);
    }

    public IReadOnlyList<JobRecord> List(int limit)
    {
        lock (_gate) return _col.Query().OrderByDescending(x => x.EnqueuedAt).Limit(limit).ToList();
    }

    public void Clear()
    {
        // keep what's still in flight; only drop finished records
        lock (_gate) _col.DeleteMany(x => x.Status == JobStatus.Succeeded || x.Status == JobStatus.Failed);
    }

    public void Dispose() => _db.Dispose();
}
