using AspNetDebugDashboard.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AspNetDebugDashboard.Extensions;

public static class HostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds AspNetDebugDashboard services to the specified IHostApplicationBuilder.
    /// </summary>
    /// <param name="builder">The IHostApplicationBuilder to add services to.</param>
    /// <param name="configure">An optional action to configure the DebugConfiguration.</param>
    /// <returns>The IHostApplicationBuilder so that additional calls can be chained.</returns>
    public static IHostApplicationBuilder AddDebugDashboard(this IHostApplicationBuilder builder, Action<DebugConfiguration>? configure = null)
    {
        builder.Services.AddDebugDashboard(configure);
        return builder;
    }
}
