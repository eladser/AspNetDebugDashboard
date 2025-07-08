using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AspNetDebugDashboard.Storage;

public static class StorageExtensions
{
    public static IServiceCollection AddLiteDbStorage(this IServiceCollection services, DebugConfiguration config)
    {
        services.AddSingleton<IDebugStorage>(provider =>
        {
            var connectionString = config.DatabasePath;
            return new LiteDbStorage(connectionString, config);
        });
        
        services.AddHostedService<StorageCleanupService>();
        
        return services;
    }
}

public class StorageCleanupService : BackgroundService
{
    private readonly IDebugStorage _storage;
    private readonly DebugConfiguration _config;
    private readonly ILogger<StorageCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(30);

    public StorageCleanupService(IDebugStorage storage, IOptions<DebugConfiguration> config, ILogger<StorageCleanupService> logger)
    {
        _storage = storage;
        _config = config.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _storage.CleanupAsync(_config.MaxEntries);
                _logger.LogDebug("Storage cleanup completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during storage cleanup");
            }
            
            await Task.Delay(_cleanupInterval, stoppingToken);
        }
    }
}
