using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using AspNetDebugDashboard.Storage;
using AspNetDebugDashboard.Interceptors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

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
        });
        
        // Register core services
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddSingleton<DebugContext>();
        services.AddScoped<IDebugLogger, DebugLogger>();
        
        // Register storage
        services.AddLiteDbStorage(config);
        
        // Register EF Core interceptor
        services.AddSingleton<DebugCommandInterceptor>();
        
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
}
