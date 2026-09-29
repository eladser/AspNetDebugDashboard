using AspNetDebugDashboard.Core.Models;
using Microsoft.AspNetCore.Hosting;

namespace AspNetDebugDashboard.Core.Services;

// Single place that decides whether the dashboard should respond to anything at all:
// enabled in config AND the current environment is on the allow list. Controllers, the
// EF interceptor, and the pipeline middleware all go through this instead of checking
// IsEnabled on their own, so there's one answer instead of three.
public static class DebugDashboardAccess
{
    // Set by the BasePath-rewrite middleware once it confirms a request actually falls under
    // the configured BasePath. Controllers check for this instead of re-deriving it themselves,
    // since by the time an action runs the path may already have been rewritten to "/_debug".
    public const string BasePathMatchedItemKey = "AspNetDebugDashboard.BasePathMatched";

    public static bool IsActive(DebugConfiguration config, string environmentName)
    {
        if (!config.IsEnabled) return false;

        return config.AllowedEnvironments.Contains("*") ||
               config.AllowedEnvironments.Contains(environmentName, StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsActive(DebugConfiguration config, IWebHostEnvironment env)
        => IsActive(config, env.EnvironmentName);
}
