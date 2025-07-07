using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using AspNetDebugDashboard.Storage;
using AspNetDebugDashboard.Interceptors;
using AspNetDebugDashboard.Web.Hubs;
using AspNetDebugDashboard.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AspNetDebugDashboard.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDebugDashboard(this IServiceCollection services, Action<DebugConfiguration>? configure = null)
    {
        var config = new DebugConfiguration();
        configure?.Invoke(config);
        
        services.Configure<DebugConfiguration>(options =>
        {
            options.IsEnabled = config.IsEnabled;
            options.DatabasePath = config.DatabasePath;
            options.BasePath = config.BasePath;
            options.MaxEntries = config.MaxEntries;
            options.LogRequestBodies = config.LogRequestBodies;
            options.LogResponseBodies = config.LogResponseBodies;
            options.LogSqlQueries = config.LogSqlQueries;
            options.LogExceptions = config.LogExceptions;
            options.EnableRealTimeUpdates = config.EnableRealTimeUpdates;
            options.ExcludedPaths = config.ExcludedPaths;
            options.ExcludedHeaders = config.ExcludedHeaders;
            options.MaxBodySize = config.MaxBodySize;
            options.RetentionPeriod = config.RetentionPeriod;
            options.EnablePerformanceCounters = config.EnablePerformanceCounters;
            options.EnableDetailedSqlLogging = config.EnableDetailedSqlLogging;
            options.AllowDataExport = config.AllowDataExport;
            options.AllowDataImport = config.AllowDataImport;
            options.SlowQueryThresholdMs = config.SlowQueryThresholdMs;
            options.SlowRequestThresholdMs = config.SlowRequestThresholdMs;
            options.TimeZone = config.TimeZone;
            options.EnableStackTraceCapture = config.EnableStackTraceCapture;
            options.MaxStackTraceDepth = config.MaxStackTraceDepth;
            options.EnableMemoryProfiling = config.EnableMemoryProfiling;
            options.EnableCpuProfiling = config.EnableCpuProfiling;
            options.CleanupInterval = config.CleanupInterval;
            options.MaxDatabaseSize = config.MaxDatabaseSize;
        });
        
        // Register core services
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddSingleton<DebugContext>();
        services.AddScoped<IDebugLogger, Core.Services.DebugLogger>();
        services.AddScoped<IDebugDashboardService, DebugDashboardService>();
        
        // Register storage
        services.AddLiteDbStorage(config);
        
        // Register EF Core interceptor
        services.AddSingleton<DebugCommandInterceptor>();
        
        // Register SignalR for real-time updates
        if (config.EnableRealTimeUpdates)
        {
            services.AddSignalR();
            services.AddScoped<IDebugDashboardNotificationService, DebugDashboardNotificationService>();
        }
        else
        {
            // Register a no-op implementation when real-time updates are disabled
            services.AddScoped<IDebugDashboardNotificationService, NoOpNotificationService>();
        }
        
        // Register background services
        if (config.IsEnabled && config.CleanupInterval.HasValue)
        {
            services.AddHostedService<DebugDashboardCleanupService>();
        }
        
        // Register health checks
        services.AddHealthChecks()
            .AddCheck<DebugDashboardHealthCheck>("debug-dashboard", 
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "debug", "dashboard" });
        
        // Add MVC services for the dashboard
        services.AddControllersWithViews()
            .AddApplicationPart(typeof(ServiceCollectionExtensions).Assembly);
        
        return services;
    }
    
    public static IServiceCollection AddDebugDashboardEntityFramework(this IServiceCollection services)
    {
        services.AddScoped<DebugCommandInterceptor>();
        return services;
    }
    
    public static IServiceCollection AddDebugDashboardSignalR(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddScoped<IDebugDashboardNotificationService, DebugDashboardNotificationService>();
        return services;
    }
    
    public static IServiceCollection AddDebugDashboardCleanup(this IServiceCollection services, TimeSpan? interval = null)
    {
        services.Configure<DebugConfiguration>(options =>
        {
            options.CleanupInterval = interval ?? TimeSpan.FromHours(1);
        });
        
        services.AddHostedService<DebugDashboardCleanupService>();
        return services;
    }
}

// No-op implementation for when real-time updates are disabled
internal class NoOpNotificationService : IDebugDashboardNotificationService
{
    public Task NotifyNewRequestAsync(RequestEntry request) => Task.CompletedTask;
    public Task NotifyNewSqlQueryAsync(SqlQueryEntry query) => Task.CompletedTask;
    public Task NotifyNewLogAsync(LogEntry log) => Task.CompletedTask;
    public Task NotifyNewExceptionAsync(ExceptionEntry exception) => Task.CompletedTask;
    public Task NotifyStatsUpdatedAsync(DebugStats stats) => Task.CompletedTask;
    public Task NotifyDataClearedAsync() => Task.CompletedTask;
}
