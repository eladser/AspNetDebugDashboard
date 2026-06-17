using System.Buffers;
using MimeKit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmtpServer;
using SmtpServer.ComponentModel;
using SmtpServer.Protocol;
using SmtpServer.Storage;

namespace AspNetMailbox;

// Maps a parsed MimeMessage to what the dashboard shows.
internal static class MailboxMapper
{
    public static MailboxMessage Map(MimeMessage m, long size)
    {
        var headers = new Dictionary<string, string>();
        foreach (var h in m.Headers) headers[h.Field] = h.Value; // last wins; avoids dup-key throw

        return new MailboxMessage
        {
            From = m.From.ToString(),
            To = m.To.Mailboxes.Select(x => x.Address).ToList(),
            Cc = m.Cc.Mailboxes.Select(x => x.Address).ToList(),
            Bcc = m.Bcc.Mailboxes.Select(x => x.Address).ToList(),
            Subject = m.Subject ?? "",
            HtmlBody = m.HtmlBody,
            TextBody = m.TextBody,
            Headers = headers,
            Attachments = m.Attachments.OfType<MimePart>().Where(p => p.Content != null).Select(p =>
            {
                using var ms = new MemoryStream();
                p.Content!.DecodeTo(ms);
                return new MailAttachment
                {
                    FileName = p.FileName ?? "attachment",
                    ContentType = p.ContentType.MimeType,
                    Content = ms.ToArray(),
                    Size = ms.Length,
                };
            }).ToList(),
            Size = size,
        };
    }
}

/// <summary>Explicit capture for senders that don't go through the SMTP sink.</summary>
public interface IMailbox
{
    void Capture(MimeMessage message);
}

internal sealed class Mailbox : IMailbox
{
    private readonly IMailboxStore _store;
    public Mailbox(IMailboxStore store) => _store = store;

    public void Capture(MimeMessage message)
    {
        using var ms = new MemoryStream();
        message.WriteTo(ms);
        var mapped = MailboxMapper.Map(message, ms.Length);
        mapped.Raw = System.Text.Encoding.Latin1.GetString(ms.ToArray());
        _store.Save(mapped);
    }
}

// Receives messages from the in-process SMTP sink and stores them.
internal sealed class SinkMessageStore : MessageStore
{
    private readonly IMailboxStore _store;
    private readonly ILogger _log;
    public SinkMessageStore(IMailboxStore store, ILogger log) { _store = store; _log = log; }

    public override async Task<SmtpResponse> SaveAsync(
        ISessionContext context, IMessageTransaction transaction,
        ReadOnlySequence<byte> buffer, CancellationToken cancellationToken)
    {
        try
        {
            using var ms = new MemoryStream();
            foreach (var segment in buffer) ms.Write(segment.Span);
            var raw = ms.ToArray();
            ms.Position = 0;
            var message = await MimeMessage.LoadAsync(ms, cancellationToken);
            var mapped = MailboxMapper.Map(message, raw.Length);
            // Latin1 round-trips raw bytes 1:1 (UTF-8 would mangle binary/8-bit parts)
            mapped.Raw = System.Text.Encoding.Latin1.GetString(raw);
            _store.Save(mapped);
        }
        catch (Exception ex)
        {
            // a malformed message must not break the dev SMTP session
            _log.LogDebug(ex, "Mailbox could not store an incoming message");
        }
        return SmtpResponse.Ok;
    }
}

// Hosts the SMTP sink for the app lifetime. Never throws into the host: a dev
// tool that can't bind its port should log and stay out of the way.
internal sealed class SmtpSinkService : BackgroundService
{
    private readonly MailboxOptions _options;
    private readonly IMailboxStore _store;
    private readonly IHostEnvironment _env;
    private readonly ILogger<SmtpSinkService> _log;

    public SmtpSinkService(MailboxOptions options, IMailboxStore store, IHostEnvironment env, ILogger<SmtpSinkService> log)
    {
        _options = options;
        _store = store;
        _env = env;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Never open a listening SMTP port in production unless explicitly asked.
        if (!_options.AlwaysRunSink && !_env.IsDevelopment())
        {
            _log.LogInformation("Mailbox SMTP sink stays off outside Development");
            return;
        }

        try
        {
            var options = new SmtpServerOptionsBuilder()
                .ServerName("aspnetmailbox")
                .Endpoint(b => b.Port(_options.SmtpPort, isSecure: false))
                .MaxMessageSize(_options.MaxMessageSizeBytes)
                .Build();

            var provider = new ServiceProvider();
            provider.Add(new SinkMessageStore(_store, _log));

            // SmtpServer (v11) isn't IDisposable; it stops when the token cancels.
            var server = new SmtpServer.SmtpServer(options, provider);
            _log.LogInformation("Mailbox SMTP sink listening on port {Port}", _options.SmtpPort);
            await server.StartAsync(stoppingToken);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Mailbox SMTP sink could not start on port {Port}", _options.SmtpPort);
        }
    }
}
