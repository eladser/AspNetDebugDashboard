using AspNetDebugDashboard.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AspNetDebugDashboard;

/// <summary>
/// Static helper class for easy logging to the debug dashboard
/// </summary>
public static class DebugLogger
{
    /// <summary>
    /// Log an info message to the debug dashboard
    /// </summary>
    public static async Task LogAsync(string message, string? tag = null, Dictionary<string, object>? properties = null)
    {
        await LogAsync(message, "Info", tag, properties);
    }
    
    /// <summary>
    /// Log a message with a specific level to the debug dashboard
    /// </summary>
    public static async Task LogAsync(string message, string level, string? tag = null, Dictionary<string, object>? properties = null)
    {
        var debugLogger = GetService<IDebugLogger>();
        
        if (debugLogger != null)
        {
            await debugLogger.LogAsync(message, level, tag, properties);
        }
    }
    
    /// <summary>
    /// Log an info message to the debug dashboard
    /// </summary>
    public static async Task InfoAsync(string message, string? tag = null, Dictionary<string, object>? properties = null)
    {
        await LogAsync(message, "Info", tag, properties);
    }
    
    /// <summary>
    /// Log a warning message to the debug dashboard
    /// </summary>
    public static async Task WarningAsync(string message, string? tag = null, Dictionary<string, object>? properties = null)
    {
        await LogAsync(message, "Warning", tag, properties);
    }
    
    /// <summary>
    /// Log an error message to the debug dashboard
    /// </summary>
    public static async Task ErrorAsync(string message, string? tag = null, Dictionary<string, object>? properties = null)
    {
        await LogAsync(message, "Error", tag, properties);
    }
    
    /// <summary>
    /// Log a success message to the debug dashboard
    /// </summary>
    public static async Task SuccessAsync(string message, string? tag = null, Dictionary<string, object>? properties = null)
    {
        await LogAsync(message, "Success", tag, properties);
    }
    
    private static T? GetService<T>() where T : class
    {
        var httpContextAccessor = ServiceProvider?.GetService<IHttpContextAccessor>();
        var httpContext = httpContextAccessor?.HttpContext;
        
        if (httpContext != null)
        {
            return httpContext.RequestServices.GetService<T>();
        }
        
        return ServiceProvider?.GetService<T>();
    }
    
    private static IServiceProvider? ServiceProvider { get; set; }
    
    internal static void SetServiceProvider(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }
}
