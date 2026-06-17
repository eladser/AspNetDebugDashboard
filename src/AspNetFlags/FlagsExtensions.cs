using AspNetDebugDashboard.Suite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AspNetFlags;

public static class FlagsExtensions
{
    private const string Icon = "<svg width=\"15\" height=\"15\" viewBox=\"0 0 16 16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M3.5 14V2.5\"/><path d=\"M3.5 3h8l-1.6 2.5L11.5 8h-8\"/></svg>";

    public static IServiceCollection AddFlags(this IServiceCollection services, Action<FlagsOptions>? configure = null)
    {
        var options = new FlagsOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton(_ => new FeatureFlags(options));
        services.AddSingleton<IFeatureFlags>(sp => sp.GetRequiredService<FeatureFlags>());
        services.AddSuitePanel(new SuitePanel("Flags", options.BasePath, Icon, 20));

        return services;
    }

    /// <summary>Serves the flags UI and API. No-op outside Development unless forceEnable is set.</summary>
    public static IApplicationBuilder UseFlags(this IApplicationBuilder app, bool forceEnable = false)
    {
        var options = app.ApplicationServices.GetService<FlagsOptions>();
        if (options is null || !options.IsEnabled) return app;

        var env = app.ApplicationServices.GetService<IWebHostEnvironment>();
        if (!forceEnable && env != null && !env.IsDevelopment()) return app;

        return app.UseMiddleware<FlagsMiddleware>(options);
    }
}
