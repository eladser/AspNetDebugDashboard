using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AspNetDebugDashboard.Services;

public class DebugDashboardCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DebugDashboardCleanupService> _logger;
    private readonly DebugConfiguration _config;

    public DebugDashboardCleanupService(
        IServiceProvider serviceProvider,
        ILogger<DebugDashboardCleanupService> logger,
        IOptions<DebugConfiguration> config)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.IsEnabled)
        {
            _logger.LogInformation("Debug Dashboard cleanup service disabled");
            return;
        }

        _logger.LogInformation("Debug Dashboard cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformCleanupAsync();
                
                // Wait for the next cleanup cycle (default: 1 hour)
                var delay = TimeSpan.FromHours(1);
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup cycle");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Wait 5 minutes before retry
            }
        }

        _logger.LogInformation("Debug Dashboard cleanup service stopped");
    }

    private async Task PerformCleanupAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IDebugStorage>();

        try
        {
            _logger.LogDebug("Starting cleanup cycle");

            // Get current counts
            var totalEntries = await storage.GetTotalEntriesAsync();
            var databaseSize = await storage.GetDatabaseSizeAsync();

            _logger.LogDebug("Current state: {TotalEntries} entries, {DatabaseSize} bytes", 
                totalEntries, databaseSize);

            var cleanedItems = 0;

            // 1. Clean by retention period (default: 7 days)
            var cutoff = DateTime.UtcNow.Subtract(TimeSpan.FromDays(7));
            var deletedByAge = await storage.DeleteOlderThanAsync(cutoff);
            cleanedItems += deletedByAge;
            
            if (deletedByAge > 0)
            {
                _logger.LogInformation("Deleted {Count} entries older than {Cutoff}", 
                    deletedByAge, cutoff);
            }

            // 2. Clean by max entries limit
            if (_config.MaxEntries > 0)
            {
                await storage.CleanupAsync(_config.MaxEntries);
                _logger.LogDebug("Applied max entries limit: {MaxEntries}", _config.MaxEntries);
            }

            // 3. Optimize database
            await storage.OptimizeAsync();

            // 4. Log results
            var newTotalEntries = await storage.GetTotalEntriesAsync();
            var newDatabaseSize = await storage.GetDatabaseSizeAsync();

            _logger.LogInformation(
                "Cleanup completed: {CleanedItems} items removed, {BeforeEntries} -> {AfterEntries} entries, {BeforeSize} -> {AfterSize} bytes",
                cleanedItems, totalEntries, newTotalEntries, databaseSize, newDatabaseSize);

            // 5. Check if we need to alert about storage issues
            if (newDatabaseSize > 100 * 1024 * 1024) // 100MB default limit
            {
                _logger.LogWarning(
                    "Database size ({CurrentSize} bytes) is getting large",
                    newDatabaseSize);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cleanup operation");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Debug Dashboard cleanup service stopping...");
        await base.StopAsync(cancellationToken);
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

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_config.IsEnabled)
            {
                return HealthCheckResult.Healthy("Debug Dashboard is disabled");
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
            
            if (databaseSize > 100 * 1024 * 1024 * 0.9) // 90% of 100MB
            {
                warnings.Add($"Database size approaching limit");
            }
            
            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                warnings.Add("Storage response time is slow");
            }

            if (warnings.Any())
            {
                data["warnings"] = warnings;
                return HealthCheckResult.Degraded("Debug Dashboard has warnings", null, data);
            }

            return HealthCheckResult.Healthy("Debug Dashboard is healthy", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            
            return HealthCheckResult.Unhealthy(
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
