using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AspNetFlags;

public static class FlagsExtensions
{
    public static IServiceCollection AddFlags(this IServiceCollection services, Action<FlagsOptions>? configure = null)
    {
        var options = new FlagsOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton(_ => new FeatureFlags(options));
        services.AddSingleton<IFeatureFlags>(sp => sp.GetRequiredService<FeatureFlags>());

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
