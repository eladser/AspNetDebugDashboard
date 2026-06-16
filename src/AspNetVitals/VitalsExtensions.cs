using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace AspNetVitals;

public static class VitalsExtensions
{
    public static IServiceCollection AddVitals(this IServiceCollection services, Action<VitalsOptions>? configure = null)
    {
        var options = new VitalsOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        // HealthCheckService is optional — only present if the app called AddHealthChecks
        services.AddSingleton(sp => new VitalsCollector(
            sp.GetRequiredService<IHostEnvironment>(),
            options,
            sp.GetService<HealthCheckService>()));

        return services;
    }

    /// <summary>Serves the vitals UI and API. No-op outside Development unless forceEnable is set.</summary>
    public static IApplicationBuilder UseVitals(this IApplicationBuilder app, bool forceEnable = false)
    {
        var options = app.ApplicationServices.GetService<VitalsOptions>();
        if (options is null || !options.IsEnabled) return app;

        var env = app.ApplicationServices.GetService<IWebHostEnvironment>();
        if (!forceEnable && env != null && !env.IsDevelopment()) return app;

        return app.UseMiddleware<VitalsMiddleware>(options);
    }
}
