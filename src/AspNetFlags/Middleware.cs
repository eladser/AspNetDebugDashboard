using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace AspNetFlags;

internal sealed class FlagsMiddleware
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static volatile string? _html;
    private static readonly object _lock = new();

    private readonly RequestDelegate _next;
    private readonly FlagsOptions _options;

    public FlagsMiddleware(RequestDelegate next, FlagsOptions options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext ctx, IFeatureFlags flags)
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

        if (rest == "/api/flags" && HttpMethods.IsGet(ctx.Request.Method))
        {
            await WriteJson(ctx, flags.All());
            return;
        }

        if (rest.StartsWith("/api/flags/"))
        {
            var name = Uri.UnescapeDataString(rest.Substring("/api/flags/".Length));
            if (string.IsNullOrEmpty(name)) { ctx.Response.StatusCode = 404; return; }
            if (name.Length > 200) { ctx.Response.StatusCode = 400; return; }

            if (HttpMethods.IsPost(ctx.Request.Method) || HttpMethods.IsPut(ctx.Request.Method))
            {
                SetRequest? body;
                try { body = await JsonSerializer.DeserializeAsync<SetRequest>(ctx.Request.Body, Json, ctx.RequestAborted); }
                catch (JsonException) { ctx.Response.StatusCode = 400; return; }
                flags.Set(name, body?.Enabled ?? false);
                await WriteJson(ctx, new { name, enabled = body?.Enabled ?? false });
                return;
            }
            if (HttpMethods.IsDelete(ctx.Request.Method))
            {
                await WriteJson(ctx, new { removed = flags.Remove(name) });
                return;
            }
        }

        ctx.Response.StatusCode = 404;
    }

    private sealed class SetRequest { public bool Enabled { get; set; } }

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
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AspNetFlags.wwwroot.index.html");
            if (stream == null) return null;
            using var reader = new StreamReader(stream);
            _html = reader.ReadToEnd();
            return _html;
        }
    }
}
