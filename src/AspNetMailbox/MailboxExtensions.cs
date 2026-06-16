using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AspNetMailbox;

public static class MailboxExtensions
{
    public static IServiceCollection AddMailbox(this IServiceCollection services, Action<MailboxOptions>? configure = null)
    {
        var options = new MailboxOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        // register the concrete type so the container disposes the LiteDB handle at shutdown
        services.AddSingleton(_ => new LiteDbMailboxStore(options));
        services.AddSingleton<IMailboxStore>(sp => sp.GetRequiredService<LiteDbMailboxStore>());
        services.AddSingleton<IMailbox, Mailbox>();

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
