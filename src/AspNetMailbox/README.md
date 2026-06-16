# AspNetMailbox

[![NuGet](https://img.shields.io/nuget/v/AspNetMailbox.svg)](https://www.nuget.org/packages/AspNetMailbox/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/eladser/AspNetDebugDashboard/blob/main/LICENSE)

Captures outbound email in-process during development and previews it at `/_mailbox`. No separate mail server, no Docker. Part of the [AspNetDebugDashboard](https://github.com/eladser/AspNetDebugDashboard) suite.

![Mailbox](https://raw.githubusercontent.com/eladser/AspNetDebugDashboard/main/docs/images/mailbox-demo.gif)

## Install

```bash
dotnet add package AspNetMailbox
```

## Setup

```csharp
using AspNetMailbox;

builder.Services.AddMailbox();   // 1. register + start the SMTP sink
var app = builder.Build();
app.UseMailbox();                // 2. serve /_mailbox (no-op outside Development)
```

Point your existing email code at the sink in Development and open `/_mailbox`.

## Capturing mail

**SMTP sink (default).** A tiny in-process SMTP server listens on port 2525. Send to `localhost:2525` from any library (MailKit, `System.Net.Mail`, anything) and it's captured. This is the Mailpit/smtp4dev model without the separate process.

```csharp
using var smtp = new SmtpClient("localhost", 2525);
smtp.Send(message);
```

**Explicit.** For senders that don't use SMTP, hand messages over directly:

```csharp
public class Notifier(IMailbox mailbox)
{
    public void Send(MimeMessage message) => mailbox.Capture(message);
}
```

## What you get

`/_mailbox` lists captured mail. Open one for tabs: rendered **Preview** (sandboxed, captured HTML can't run scripts in your dashboard), **HTML**, **Text**, **Headers**, **Raw** source, and **Attachments** (downloadable). Search by subject or sender.

## Configuration

```csharp
builder.Services.AddMailbox(o =>
{
    o.BasePath = "/_mailbox";       // dashboard route
    o.SmtpPort = 2525;              // sink port
    o.EnableSmtpSink = true;        // turn the sink off if you only capture explicitly
    o.MaxMessages = 200;            // oldest trimmed past this
    o.DatabasePath = "mailbox.db";  // local LiteDB store
});
```

## License

MIT.
