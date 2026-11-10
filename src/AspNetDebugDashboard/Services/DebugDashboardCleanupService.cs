using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AspNetDebugDashboard.Services;

public class DebugDashboardCleanupService : BackgroundService
{
    private const int MaxCompactionPasses = 4;
    private const double DeleteFractionOnOverflow = 0.25;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DebugDashboardCleanupService> _logger;
    private readonly DebugConfiguration _config;
    private readonly IWebHostEnvironment? _env;

    public DebugDashboardCleanupService(
        IServiceProvider serviceProvider,
        ILogger<DebugDashboardCleanupService> logger,
        IOptions<DebugConfiguration> config,
        IWebHostEnvironment? env = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _config = config.Value;
        _env = env;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Never resolve storage outside the allowed environments - that's what created
        // the db file in Production before (LiteDbStorage is a lazy singleton).
        if (_env == null || !DebugDashboardAccess.IsActive(_config, _env))
        {
            _logger.LogInformation("Debug Dashboard cleanup service is not active for this environment");
            return;
        }

        _logger.LogInformation("Debug Dashboard cleanup service started");

        var interval = _config.CleanupInterval ?? TimeSpan.FromHours(1);

        while (!stoppingToken.IsCancellationRequested)
        {
            // net11's IHost.RunAsync throws on a failed BackgroundService, so nothing here
            // is allowed to escape this loop.
            try
            {
                await PerformCleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup cycle");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Debug Dashboard cleanup service stopped");
    }

    private async Task PerformCleanupAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IDebugStorage>();

        var cutoff = DateTime.UtcNow.Subtract(_config.RetentionPeriod);
        var deletedByAge = await storage.DeleteOlderThanAsync(cutoff);
        if (deletedByAge > 0)
        {
            _logger.LogInformation("Deleted {Count} entries older than {Cutoff}", deletedByAge, cutoff);
        }

        if (_config.MaxEntries > 0)
        {
            await storage.CleanupAsync(_config.MaxEntries);
        }

        // Checkpoint to flush the WAL before measuring size - otherwise GetDatabaseSizeAsync
        // undercounts everything still sitting in the -log.db file.
        await storage.OptimizeAsync();
        var size = await storage.GetDatabaseSizeAsync();

        var passes = 0;
        while (size > _config.MaxDatabaseSize && passes < MaxCompactionPasses)
        {
            stoppingToken.ThrowIfCancellationRequested();

            await storage.DeleteOldestFractionAsync(DeleteFractionOnOverflow);
            await storage.CompactAsync();
            size = await storage.GetDatabaseSizeAsync();
            passes++;
        }

        if (size > _config.MaxDatabaseSize)
        {
            _logger.LogWarning(
                "Database size ({Size} bytes) still exceeds MaxDatabaseSize ({Max} bytes) after {Passes} compaction passes",
                size, _config.MaxDatabaseSize, passes);
        }
    }
}

public class DebugDashboardHealthCheck : IHealthCheck
{
    private readonly IDebugStorage _storage;
    private readonly DebugConfiguration _config;
    private readonly ILogger<DebugDashboardHealthCheck> _logger;

    public DebugDashboardHealthCheck(
        IDebugStorage storage,
        IOptions<DebugConfiguration> config,
        ILogger<DebugDashboardHealthCheck> logger)
    {
        _storage = storage;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_config.IsEnabled)
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Debug Dashboard is disabled");
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Test storage connectivity
            var stats = await _storage.GetStatsAsync();
            var totalEntries = await _storage.GetTotalEntriesAsync();
            var databaseSize = await _storage.GetDatabaseSizeAsync();

            stopwatch.Stop();

            var data = new Dictionary<string, object>
            {
                ["total_entries"] = totalEntries,
                ["database_size_bytes"] = databaseSize,
                ["response_time_ms"] = stopwatch.ElapsedMilliseconds,
                ["max_entries"] = _config.MaxEntries
            };

            // Check for warning conditions
            var warnings = new List<string>();

            if (totalEntries > _config.MaxEntries * 0.9)
            {
                warnings.Add($"Entry count ({totalEntries}) approaching limit ({_config.MaxEntries})");
            }

            if (databaseSize > _config.MaxDatabaseSize * 0.9)
            {
                warnings.Add("Database size approaching limit");
            }

            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                warnings.Add("Storage response time is slow");
            }

            if (warnings.Any())
            {
                data["warnings"] = warnings;
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded("Debug Dashboard has warnings", null, data);
            }

            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Debug Dashboard is healthy", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");

            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                "Debug Dashboard health check failed",
                ex,
                new Dictionary<string, object>
                {
                    ["error"] = ex.Message,
                    ["enabled"] = _config.IsEnabled
                });
        }
    }
}
