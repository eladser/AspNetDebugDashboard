using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using AspNetDebugDashboard.Extensions;
using AspNetDebugDashboard.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace AspNetDebugDashboard.Tests;

public class CleanupServiceTests : IDisposable
{
    private readonly List<string> _tempPaths = new();

    private string NewTempDbPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"debugdash-cleanup-{Guid.NewGuid():N}.db");
        _tempPaths.Add(path);
        return path;
    }

    private static ServiceProvider BuildProvider(string environmentName, Action<DebugConfiguration> configure)
    {
        var services = new ServiceCollection();
        services.Configure<HealthCheckServiceOptions>(o => o.Registrations.Clear());
        services.AddDebugDashboard(configure);
        services.AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment { EnvironmentName = environmentName });
        return services.BuildServiceProvider();
    }

    private static DebugDashboardCleanupService GetCleanupService(ServiceProvider provider)
        => provider.GetServices<IHostedService>().OfType<DebugDashboardCleanupService>().Single();

    [Fact]
    public async Task PerformCleanup_HonorsRetentionPeriod()
    {
        var dbPath = NewTempDbPath();
        using var provider = BuildProvider("Development", o =>
        {
            o.DatabasePath = dbPath;
            o.RetentionPeriod = TimeSpan.FromDays(1);
            o.CleanupInterval = TimeSpan.FromMinutes(30);
            o.MaxDatabaseSize = long.MaxValue;
        });

        var storage = provider.GetRequiredService<IDebugStorage>();
        await storage.StoreRequestAsync(new RequestEntry { Timestamp = DateTime.UtcNow.AddDays(-5) });
        await storage.StoreRequestAsync(new RequestEntry { Timestamp = DateTime.UtcNow });

        var service = GetCleanupService(provider);
        await service.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(async () => await storage.GetTotalEntriesAsync() == 1, TimeSpan.FromSeconds(5));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        (await storage.GetTotalEntriesAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_RepeatsAtConfiguredCleanupInterval()
    {
        var dbPath = NewTempDbPath();
        using var provider = BuildProvider("Development", o =>
        {
            o.DatabasePath = dbPath;
            o.RetentionPeriod = TimeSpan.FromMilliseconds(1);
            o.CleanupInterval = TimeSpan.FromMilliseconds(100);
            o.MaxDatabaseSize = long.MaxValue;
        });

        var storage = provider.GetRequiredService<IDebugStorage>();
        var service = GetCleanupService(provider);
        await service.StartAsync(CancellationToken.None);
        try
        {
            // Let the first pass run, then add an entry only a second pass can catch -
            // if CleanupInterval were ignored (the old hardcoded 1h loop), this times out.
            await Task.Delay(150);
            await storage.StoreRequestAsync(new RequestEntry { Timestamp = DateTime.UtcNow.AddDays(-1) });
            await WaitUntilAsync(async () => await storage.GetTotalEntriesAsync() == 0, TimeSpan.FromSeconds(5));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        (await storage.GetTotalEntriesAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_InProduction_NeverCreatesDatabaseFile()
    {
        var dbPath = NewTempDbPath();
        using var provider = BuildProvider("Production", o =>
        {
            o.DatabasePath = dbPath;
            // AllowedEnvironments defaults to Development-only, so this should be a full no-op.
        });

        var service = GetCleanupService(provider);
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await service.StopAsync(CancellationToken.None);

        File.Exists(dbPath).Should().BeFalse();
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        while (!await condition())
        {
            if (DateTime.UtcNow - start > timeout) throw new TimeoutException("condition not met in time");
            await Task.Delay(25);
        }
    }

    public void Dispose()
    {
        foreach (var path in _tempPaths)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                var logPath = Path.Combine(
                    Path.GetDirectoryName(path)!,
                    Path.GetFileNameWithoutExtension(path) + "-log" + Path.GetExtension(path));
                if (File.Exists(logPath)) File.Delete(logPath);
            }
            catch
            {
                // stale LiteDB file handles on Windows aren't worth failing the run over
            }
        }
    }

    private class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
