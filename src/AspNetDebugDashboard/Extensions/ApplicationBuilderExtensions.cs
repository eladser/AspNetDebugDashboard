using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
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

        if (!DebugDashboardAccess.IsActive(config, env))
        {
            return app;
        }

        return ConfigureDebugDashboard(app);
    }

    public static IApplicationBuilder UseDebugDashboard(this IApplicationBuilder app, bool forceEnable)
    {
        var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
        var config = app.ApplicationServices.GetRequiredService<IOptions<DebugConfiguration>>().Value;

        // forceEnable is an explicit opt-in: turn the dashboard on and add the current
        // environment to the allow list, so the controllers and interceptor agree with the
        // pipeline instead of still 404ing behind it.
        if (forceEnable)
        {
            config.IsEnabled = true;
            if (!config.AllowedEnvironments.Contains(env.EnvironmentName, StringComparer.OrdinalIgnoreCase))
            {
                config.AllowedEnvironments.Add(env.EnvironmentName);
            }
        }

        if (!DebugDashboardAccess.IsActive(config, env))
        {
            return app;
        }

        return ConfigureDebugDashboard(app);
    }

    public static IApplicationBuilder UseDebugDashboard(this IApplicationBuilder app, Action<DebugConfiguration> configure)
    {
        var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
        var config = app.ApplicationServices.GetRequiredService<IOptions<DebugConfiguration>>().Value;
        configure(config);

        if (!DebugDashboardAccess.IsActive(config, env))
        {
            return app;
        }

        return ConfigureDebugDashboard(app);
    }

    private static IApplicationBuilder ConfigureDebugDashboard(IApplicationBuilder app)
    {
        // Rewrite a custom BasePath onto the canonical "/_debug" prefix the controllers'
        // route attributes match on, so any configured BasePath actually works.
        app.Use(async (context, next) =>
        {
            var config = context.RequestServices.GetRequiredService<IOptions<DebugConfiguration>>().Value;
            var basePath = config.BasePath.TrimEnd('/');

            if (basePath.Length > 0 && context.Request.Path.StartsWithSegments(basePath, out var remaining))
            {
                context.Items[DebugDashboardAccess.BasePathMatchedItemKey] = true;

                if (basePath != "/_debug")
                {
                    context.Request.Path = "/_debug" + remaining;
                }
            }

            await next();
        });

        // Add exception middleware first (should be early in pipeline)
        app.UseMiddleware<DebugExceptionMiddleware>();

        // Add request logging middleware (should be early but after exception handling)
        app.UseMiddleware<DebugRequestMiddleware>();

        // Ensure routing is available for the debug dashboard controllers
        // This adds the necessary routing infrastructure if it hasn't been added yet
        var services = app.ApplicationServices;
        var endpointRouteBuilder = app.ApplicationServices.GetService<Microsoft.AspNetCore.Routing.IEndpointRouteBuilder>();

        if (endpointRouteBuilder == null)
        {
            // If routing hasn't been configured yet, we need to add it
            app.UseRouting();
        }

        return app;
    }
}
