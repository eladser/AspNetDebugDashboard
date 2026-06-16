using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace AspNetMailbox;

// Terminal middleware: serves the embedded page at BasePath and JSON under BasePath/api.
internal sealed class MailboxMiddleware
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string? _html;
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

        if (!path.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
        {
            await _next(ctx);
            return;
        }

        var rest = path.Substring(basePath.Length).TrimEnd('/');

        // page
        if (rest.Length == 0)
        {
            var html = LoadHtml();
            if (html == null) { ctx.Response.StatusCode = 404; return; }
            ctx.Response.ContentType = "text/html";
            await ctx.Response.WriteAsync(html.Replace("%BASE_PATH%", basePath));
            return;
        }

        // api
        if (rest == "/api/messages")
        {
            var search = ctx.Request.Query["search"].FirstOrDefault();
            int.TryParse(ctx.Request.Query["page"], out var page); if (page < 1) page = 1;
            int.TryParse(ctx.Request.Query["pageSize"], out var size); if (size < 1) size = 50;
            var total = store.Count(search);
            var items = store.List(search, (page - 1) * size, size).Select(Summary);
            await WriteJson(ctx, new { items, totalCount = total, page, pageSize = size });
            return;
        }

        if (rest.StartsWith("/api/messages/"))
        {
            var tail = rest.Substring("/api/messages/".Length);
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
                ctx.Response.ContentType = att.ContentType;
                ctx.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{att.FileName}\"";
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
