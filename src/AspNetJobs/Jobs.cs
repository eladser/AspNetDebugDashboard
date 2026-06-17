using LiteDB;
using System.Text.Json.Serialization;

namespace AspNetJobs;

public enum JobStatus { Pending, Running, Succeeded, Failed }

public class JobRecord
{
    [BsonId, JsonIgnore]
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    public string JobId { get; set; } = "";
    public string Name { get; set; } = "";
    public JobStatus Status { get; set; }
    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public double? DurationMs { get; set; }
    public string? Error { get; set; }
}

public class JobsOptions
{
    public bool IsEnabled { get; set; } = true;
    public string BasePath { get; set; } = "/_jobs";
    public string DatabasePath { get; set; } = "jobs.db";
    public int MaxRecords { get; set; } = 500;
}

/// <summary>Enqueues background work. Inject this and call Enqueue from anywhere.</summary>
public interface IJobQueue
{
    /// <summary>Queue a unit of work. Returns the job id used to track it.</summary>
    string Enqueue(string name, Func<CancellationToken, Task> work);
}
