using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace AspNetMailbox;

// Terminal middleware: serves the embedded page at BasePath and JSON under BasePath/api.
internal sealed class MailboxMiddleware
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static volatile string? _html;
    private static readonly object _lock = new();

    private readonly RequestDelegate _next;
    private readonly MailboxOptions _options;

    public MailboxMiddleware(RequestDelegate next, MailboxOptions options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext ctx, IMailboxStore store)
    {
        var path = ctx.Request.Path.Value ?? "";
        var basePath = _options.BasePath;

        // exact base or a child path only, so /_mailbox-other doesn't match
        if (!(path.Equals(basePath, StringComparison.OrdinalIgnoreCase)
              || path.StartsWith(basePath + "/", StringComparison.OrdinalIgnoreCase)))
        {
            await _next(ctx);
            return;
        }

        var rest = path.Substring(basePath.Length).TrimEnd('/');

        // captured mail is untrusted; never let a response be sniffed into an
        // executable type in the dashboard's origin
        ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
        ctx.Response.Headers["X-Frame-Options"] = "DENY";

        // page
        if (rest.Length == 0)
        {
            var html = LoadHtml();
            if (html == null) { ctx.Response.StatusCode = 404; return; }
            ctx.Response.ContentType = "text/html";
            var nav = AspNetDebugDashboard.Suite.SuiteNav.BuildJson(ctx.RequestServices, basePath);
            await ctx.Response.WriteAsync(html.Replace("%BASE_PATH%", basePath).Replace("%SUITE_NAV%", nav));
            return;
        }

        // api
        if (rest == "/api/messages")
        {
            var search = ctx.Request.Query["search"].FirstOrDefault();
            int.TryParse(ctx.Request.Query["page"], out var page); if (page < 1) page = 1;
            int.TryParse(ctx.Request.Query["pageSize"], out var size); if (size < 1) size = 50; if (size > 500) size = 500;
            var total = store.Count(search);
            var items = store.List(search, (page - 1) * size, size).Select(Summary);
            await WriteJson(ctx, new { items, totalCount = total, page, pageSize = size });
            return;
        }

        if (rest.StartsWith("/api/messages/"))
        {
            var tail = rest.Substring("/api/messages/".Length);

            // raw .eml download: {id}/eml
            if (tail.EndsWith("/eml", StringComparison.Ordinal))
            {
                var id = tail.Substring(0, tail.Length - "/eml".Length);
                var msg = store.Get(id);
                if (msg == null) { ctx.Response.StatusCode = 404; return; }
                ctx.Response.ContentType = "message/rfc822";
                var fname = SafeFileName(string.IsNullOrWhiteSpace(msg.Subject) ? id : msg.Subject) + ".eml";
                ctx.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fname}\"";
                // Raw was captured via Latin1 to preserve bytes; round-trip the same way
                await ctx.Response.Body.WriteAsync(System.Text.Encoding.Latin1.GetBytes(msg.Raw ?? ""));
                return;
            }

            // attachment download: {id}/attachments/{index}
            var attIdx = tail.IndexOf("/attachments/", StringComparison.Ordinal);
            if (attIdx >= 0)
            {
                var id = tail.Substring(0, attIdx);
                var idxStr = tail.Substring(attIdx + "/attachments/".Length);
                var msg = store.Get(id);
                if (msg == null || !int.TryParse(idxStr, out var i) || i < 0 || i >= msg.Attachments.Count)
                { ctx.Response.StatusCode = 404; return; }
                var att = msg.Attachments[i];
                // force a download rather than serving the attacker-supplied Content-Type
                // (a text/html or SVG attachment would otherwise run in this origin)
                ctx.Response.ContentType = "application/octet-stream";
                ctx.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{SafeFileName(att.FileName)}\"";
                await ctx.Response.Body.WriteAsync(att.Content);
                return;
            }

            var message = store.Get(tail);
            if (message == null) { ctx.Response.StatusCode = 404; return; }
            await WriteJson(ctx, Detail(message));
            return;
        }

        if (rest == "/api/clear" && HttpMethods.IsDelete(ctx.Request.Method))
        {
            store.Clear();
            await WriteJson(ctx, new { cleared = true });
            return;
        }

        ctx.Response.StatusCode = 404;
    }

    // List rows stay light: no bodies, no attachment bytes.
    private static object Summary(MailboxMessage m) => new
    {
        m.Id, m.ReceivedAt, m.From, m.To, m.Subject,
        attachments = m.Attachments.Count,
        hasHtml = m.HtmlBody != null,
    };

    // Detail keeps everything except attachment bytes (those download separately).
    private static object Detail(MailboxMessage m) => new
    {
        m.Id, m.ReceivedAt, m.From, m.To, m.Cc, m.Bcc, m.Subject,
        m.HtmlBody, m.TextBody, m.Headers, m.Size, m.Raw,
        attachments = m.Attachments.Select((a, i) => new { index = i, a.FileName, a.ContentType, a.Size }),
    };

    // ASCII-only download filename: non-ASCII bytes are invalid in an HTTP header (Kestrel throws),
    // and dropping ; = " CR LF prevents Content-Disposition parsing tricks / header injection.
    private static string SafeFileName(string s)
    {
        var clean = new string(s.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or ' ' or '.' ? c : '_').ToArray()).Trim();
        if (clean.Length > 80) clean = clean.Substring(0, 80);
        return clean.Length == 0 ? "message" : clean;
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
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("AspNetMailbox.wwwroot.index.html");
            if (stream == null) return null;
            using var reader = new StreamReader(stream);
            _html = reader.ReadToEnd();
            return _html;
        }
    }
}
