using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace AspNetJobs;

internal sealed class JobsMiddleware
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
    private static volatile string? _html;
    private static readonly object _lock = new();

    private readonly RequestDelegate _next;
    private readonly JobsOptions _options;

    public JobsMiddleware(RequestDelegate next, JobsOptions options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext ctx, IJobStore store)
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
        ctx.Response.Headers["X-Frame-Options"] = "DENY";       // stack traces aren't for framing
        ctx.Response.Headers["Cache-Control"] = "no-store";
        var rest = path.Substring(basePath.Length).TrimEnd('/');

        if (rest.Length == 0)
        {
            var html = LoadHtml();
            if (html == null) { ctx.Response.StatusCode = 404; return; }
            ctx.Response.ContentType = "text/html; charset=utf-8";
            await ctx.Response.WriteAsync(html.Replace("%BASE_PATH%", basePath));
            return;
        }

        if (rest == "/api/jobs" && HttpMethods.IsGet(ctx.Request.Method))
        {
            var limit = 200;
            if (int.TryParse(ctx.Request.Query["limit"], out var l)) limit = Math.Clamp(l, 1, 1000);
            await WriteJson(ctx, store.List(limit));
            return;
        }

        if (rest == "/api/clear" && HttpMethods.IsDelete(ctx.Request.Method))
        {
            store.Clear();
            ctx.Response.StatusCode = 204;
            return;
        }

        ctx.Response.StatusCode = 404;
    }

    private static async Task WriteJson(HttpContext ctx, object value)
    {
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(value, Json));
    }

    private static string? LoadHtml()
    {
        if (_html != null) return _html;
        lock (_lock)
        {
            if (_html != null) return _html;
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AspNetJobs.wwwroot.index.html");
            if (stream == null) return null;
            using var reader = new StreamReader(stream);
            _html = reader.ReadToEnd();
            return _html;
        }
    }
}
