using AspNetDebugDashboard.Suite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AspNetMailbox;

public static class MailboxExtensions
{
    private const string Icon = "<svg width=\"15\" height=\"15\" viewBox=\"0 0 16 16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><rect x=\"1.5\" y=\"3\" width=\"13\" height=\"10\" rx=\"1.5\"/><path d=\"m2 4.5 6 4.5 6-4.5\"/></svg>";

    public static IServiceCollection AddMailbox(this IServiceCollection services, Action<MailboxOptions>? configure = null)
    {
        var options = new MailboxOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        // register the concrete type so the container disposes the LiteDB handle at shutdown
        services.AddSingleton(_ => new LiteDbMailboxStore(options));
        services.AddSingleton<IMailboxStore>(sp => sp.GetRequiredService<LiteDbMailboxStore>());
        services.AddSingleton<IMailbox, Mailbox>();
        services.AddSuitePanel(new SuitePanel("Mailbox", options.BasePath, Icon, 10));

        if (options.IsEnabled && options.EnableSmtpSink)
        {
            services.AddSingleton<IHostedService, SmtpSinkService>();
        }

        return services;
    }

    /// <summary>Serves the mailbox UI and API. No-op outside Development unless forceEnable is set.</summary>
    public static IApplicationBuilder UseMailbox(this IApplicationBuilder app, bool forceEnable = false)
    {
        var options = app.ApplicationServices.GetService<MailboxOptions>();
        if (options is null || !options.IsEnabled) return app;

        var env = app.ApplicationServices.GetService<IWebHostEnvironment>();
        if (!forceEnable && env != null && !env.IsDevelopment()) return app;

        return app.UseMiddleware<MailboxMiddleware>(options);
    }
}
