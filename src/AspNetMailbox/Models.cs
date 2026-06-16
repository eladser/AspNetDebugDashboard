namespace AspNetMailbox;

public class MailboxMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public string From { get; set; } = "";
    public List<string> To { get; set; } = new();
    public List<string> Cc { get; set; } = new();
    public List<string> Bcc { get; set; } = new();
    public string Subject { get; set; } = "";
    public string? HtmlBody { get; set; }
    public string? TextBody { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public List<MailAttachment> Attachments { get; set; } = new();
    public long Size { get; set; }
    public string? Raw { get; set; }
}

public class MailAttachment
{
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public long Size { get; set; }
    public byte[] Content { get; set; } = Array.Empty<byte>();
}

public class MailboxOptions
{
    public bool IsEnabled { get; set; } = true;

    // Own root so it doesn't collide with the dashboard's /_debug catch-all when
    // both are installed. The unified shell aligns these later.
    public string BasePath { get; set; } = "/_mailbox";

    public string DatabasePath { get; set; } = "mailbox.db";
    public int MaxMessages { get; set; } = 200;

    // The in-process SMTP sink. Point your sender at localhost:SmtpPort in dev.
    public bool EnableSmtpSink { get; set; } = true;
    public int SmtpPort { get; set; } = 2525;
}
