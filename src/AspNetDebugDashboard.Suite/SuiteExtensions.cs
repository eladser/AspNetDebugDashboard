using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace AspNetDebugDashboard.Suite;

public static class SuiteExtensions
{
    /// <summary>Advertise this tool to the shared suite sidebar.</summary>
    public static IServiceCollection AddSuitePanel(this IServiceCollection services, SuitePanel panel)
    {
        services.AddSingleton(panel);
        return services;
    }
}

public static class SuiteNav
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Builds the JSON for <c>window.__SUITE_NAV__</c>: the installed panels (deduped by route, ordered)
    /// plus the route of the page being served, so the sidebar can highlight the active tool.
    /// </summary>
    public static string BuildJson(IServiceProvider services, string currentRoute)
    {
        var panels = services.GetServices<SuitePanel>()
            .GroupBy(p => p.Route)
            .Select(g => g.First())
            .OrderBy(p => p.Order)
            .Select(p => new { name = p.Name, route = p.Route, icon = p.Icon });
        return JsonSerializer.Serialize(new { current = currentRoute, panels }, Json);
    }
}
