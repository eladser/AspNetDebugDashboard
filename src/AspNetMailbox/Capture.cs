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
        mapped.Raw = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        _store.Save(mapped);
    }
}

// Receives messages from the in-process SMTP sink and stores them.
internal sealed class SinkMessageStore : MessageStore
{
    private readonly IMailboxStore _store;
    public SinkMessageStore(IMailboxStore store) => _store = store;

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
            mapped.Raw = System.Text.Encoding.UTF8.GetString(raw);
            _store.Save(mapped);
        }
        catch
        {
            // a malformed message must not break the dev SMTP session
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
    private readonly ILogger<SmtpSinkService> _log;

    public SmtpSinkService(MailboxOptions options, IMailboxStore store, ILogger<SmtpSinkService> log)
    {
        _options = options;
        _store = store;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var options = new SmtpServerOptionsBuilder()
                .ServerName("aspnetmailbox")
                .Endpoint(b => b.Port(_options.SmtpPort, isSecure: false))
                .Build();

            var provider = new ServiceProvider();
            provider.Add(new SinkMessageStore(_store));

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
