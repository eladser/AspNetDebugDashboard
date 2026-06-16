using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AspNetJobs;

public static class JobsExtensions
{
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
