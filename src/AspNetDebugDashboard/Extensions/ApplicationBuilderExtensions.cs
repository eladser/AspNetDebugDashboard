using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AspNetDebugDashboard.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseDebugDashboard(this IApplicationBuilder app)
    {
        var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
        var config = app.ApplicationServices.GetRequiredService<IOptions<DebugConfiguration>>().Value;
        
        if (!config.IsEnabled)
        {
            return app;
        }
        
        // Only enable in development environment by default
        if (!env.IsDevelopment())
        {
            return app;
        }
        
        return ConfigureDebugDashboard(app);
    }
    
    public static IApplicationBuilder UseDebugDashboard(this IApplicationBuilder app, bool forceEnable)
    {
        var config = app.ApplicationServices.GetRequiredService<IOptions<DebugConfiguration>>().Value;
        
        if (!config.IsEnabled && !forceEnable)
        {
            return app;
        }
        
        return ConfigureDebugDashboard(app);
    }
    
    public static IApplicationBuilder UseDebugDashboard(this IApplicationBuilder app, Action<DebugConfiguration> configure)
    {
        var config = app.ApplicationServices.GetRequiredService<IOptions<DebugConfiguration>>().Value;
        configure(config);
        
        if (!config.IsEnabled)
        {
            return app;
        }
        
        return ConfigureDebugDashboard(app);
    }
    
    private static IApplicationBuilder ConfigureDebugDashboard(IApplicationBuilder app)
    {
        // Add exception middleware first (should be early in pipeline)
        app.UseMiddleware<DebugExceptionMiddleware>();
        
        // Add request logging middleware (should be early but after exception handling)
        app.UseMiddleware<DebugRequestMiddleware>();
        
        return app;
    }
}
