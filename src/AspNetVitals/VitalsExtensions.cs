using AspNetDebugDashboard.Suite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace AspNetVitals;

public static class VitalsExtensions
{
    private const string Icon = "<svg width=\"15\" height=\"15\" viewBox=\"0 0 16 16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M1.5 8h3l2-4.5L10 12l2-4h2.5\"/></svg>";

    public static IServiceCollection AddVitals(this IServiceCollection services, Action<VitalsOptions>? configure = null)
    {
        var options = new VitalsOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        // HealthCheckService is optional, present only if the app called AddHealthChecks
        services.AddSingleton(sp => new VitalsCollector(
            sp.GetRequiredService<IHostEnvironment>(),
            options,
            sp.GetService<HealthCheckService>()));
        services.AddSuitePanel(new SuitePanel("Vitals", options.BasePath, Icon, 40));

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
