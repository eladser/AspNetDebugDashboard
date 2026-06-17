using AspNetDebugDashboard.Suite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AspNetJobs;

public static class JobsExtensions
{
    private const string Icon = "<svg width=\"15\" height=\"15\" viewBox=\"0 0 16 16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.5\" stroke-linecap=\"round\"><rect x=\"2\" y=\"3\" width=\"12\" height=\"3.2\" rx=\"1\"/><rect x=\"2\" y=\"9.8\" width=\"12\" height=\"3.2\" rx=\"1\"/></svg>";

    public static IServiceCollection AddJobs(this IServiceCollection services, Action<JobsOptions>? configure = null)
    {
        var options = new JobsOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<LiteDbJobStore>();
        services.AddSingleton<IJobStore>(sp => sp.GetRequiredService<LiteDbJobStore>());

        // one JobQueue instance is both the IJobQueue producer and the hosted runner
        services.AddSingleton<JobQueue>();
        services.AddSingleton<IJobQueue>(sp => sp.GetRequiredService<JobQueue>());
        services.AddHostedService(sp => sp.GetRequiredService<JobQueue>());
        services.AddSuitePanel(new SuitePanel("Jobs", options.BasePath, Icon, 30));

        return services;
    }

    /// <summary>Serves the jobs UI and API. No-op outside Development unless forceEnable is set.</summary>
    public static IApplicationBuilder UseJobs(this IApplicationBuilder app, bool forceEnable = false)
    {
        var options = app.ApplicationServices.GetService<JobsOptions>();
        if (options is null || !options.IsEnabled) return app;

        var env = app.ApplicationServices.GetService<IWebHostEnvironment>();
        if (!forceEnable && env != null && !env.IsDevelopment()) return app;

        return app.UseMiddleware<JobsMiddleware>(options);
    }
}
