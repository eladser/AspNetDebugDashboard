using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace AspNetVitals;

public class VitalsOptions
{
    public bool IsEnabled { get; set; } = true;
    public string BasePath { get; set; } = "/_vitals";

    // Health-check exception messages can carry connection strings and secrets, so
    // they're hidden by default. The author-supplied Description is always shown.
    public bool IncludeExceptionDetails { get; set; }
}

public sealed class HealthEntry
{
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";   // Healthy / Degraded / Unhealthy
    public string? Description { get; set; }
    public double DurationMs { get; set; }
}

public sealed class VitalsSnapshot
{
    public double UptimeSeconds { get; set; }
    public long ManagedMemoryBytes { get; set; }
    public long WorkingSetBytes { get; set; }
    public int Gen0 { get; set; }
    public int Gen1 { get; set; }
    public int Gen2 { get; set; }
    public int ThreadCount { get; set; }
    public int ProcessorCount { get; set; }
    public string Runtime { get; set; } = "";
    public string Environment { get; set; } = "";
    public string? OverallHealth { get; set; }   // null when no health checks are registered
    public List<HealthEntry> HealthChecks { get; set; } = new();
}

internal sealed class VitalsCollector
{
    private static readonly Process Proc = Process.GetCurrentProcess();
    private static readonly TimeSpan Ttl = TimeSpan.FromMilliseconds(1500);

    private readonly IHostEnvironment _env;
    private readonly HealthCheckService? _health;
    private readonly VitalsOptions _options;

    // ponytail: instance cache, no lock. Worst case two concurrent polls recompute.
    private VitalsSnapshot? _cache;
    private DateTime _cacheAt;

    public VitalsCollector(IHostEnvironment env, VitalsOptions options, HealthCheckService? health = null)
    {
        _env = env;
        _options = options;
        _health = health;
    }

    public async Task<VitalsSnapshot> Collect(CancellationToken ct)
    {
        if (_cache != null && DateTime.UtcNow - _cacheAt < Ttl) return _cache;

        var snap = new VitalsSnapshot
        {
            ManagedMemoryBytes = GC.GetTotalMemory(false),
            Gen0 = GC.CollectionCount(0),
            Gen1 = GC.CollectionCount(1),
            Gen2 = GC.CollectionCount(2),
            ProcessorCount = Environment.ProcessorCount,
            Runtime = RuntimeInformation.FrameworkDescription,
            Environment = _env.EnvironmentName,
        };

        // Reading proc.* can throw on hardened/locked-down Linux containers (no /proc access).
        try
        {
            Proc.Refresh();
            snap.UptimeSeconds = (DateTime.UtcNow - Proc.StartTime.ToUniversalTime()).TotalSeconds;
            snap.WorkingSetBytes = Proc.WorkingSet64;
            snap.ThreadCount = Proc.Threads.Count;
        }
        catch { /* metrics unavailable in this environment */ }

        if (_health != null)
        {
            var report = await _health.CheckHealthAsync(ct);
            snap.OverallHealth = report.Status.ToString();
            foreach (var (name, e) in report.Entries)
                snap.HealthChecks.Add(new HealthEntry
                {
                    Name = name,
                    Status = e.Status.ToString(),
                    Description = e.Description ?? (_options.IncludeExceptionDetails ? e.Exception?.Message : null),
                    DurationMs = e.Duration.TotalMilliseconds,
                });
        }

        _cache = snap;
        _cacheAt = DateTime.UtcNow;
        return snap;
    }
}
