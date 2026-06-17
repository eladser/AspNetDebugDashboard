using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace AspNetVitals;

internal sealed class VitalsMiddleware
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static volatile string? _html;
    private static readonly object _lock = new();

    private readonly RequestDelegate _next;
    private readonly VitalsOptions _options;

    public VitalsMiddleware(RequestDelegate next, VitalsOptions options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext ctx, VitalsCollector collector)
    {
        var path = ctx.Request.Path.Value ?? "";
        var basePath = _options.BasePath;

        if (!(path.Equals(basePath, StringComparison.OrdinalIgnoreCase)
              || path.StartsWith(basePath + "/", StringComparison.OrdinalIgnoreCase)))
        {
            await _next(ctx);
            return;
        }

        ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
        ctx.Response.Headers["X-Frame-Options"] = "DENY";
        ctx.Response.Headers["Cache-Control"] = "no-store";
        var rest = path.Substring(basePath.Length).TrimEnd('/');

        if (rest.Length == 0)
        {
            var html = LoadHtml();
            if (html == null) { ctx.Response.StatusCode = 404; return; }
            ctx.Response.ContentType = "text/html; charset=utf-8";
            var nav = AspNetDebugDashboard.Suite.SuiteNav.BuildJson(ctx.RequestServices, basePath);
            await ctx.Response.WriteAsync(html.Replace("%BASE_PATH%", basePath).Replace("%SUITE_NAV%", nav));
            return;
        }

        if (rest == "/api/vitals" && HttpMethods.IsGet(ctx.Request.Method))
        {
            var snap = await collector.Collect(ctx.RequestAborted);
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(snap, Json));
            return;
        }

        ctx.Response.StatusCode = 404;
    }

    private static string? LoadHtml()
    {
        if (_html != null) return _html;
        lock (_lock)
        {
            if (_html != null) return _html;
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AspNetVitals.wwwroot.index.html");
            if (stream == null) return null;
            using var reader = new StreamReader(stream);
            _html = reader.ReadToEnd();
            return _html;
        }
    }
}
