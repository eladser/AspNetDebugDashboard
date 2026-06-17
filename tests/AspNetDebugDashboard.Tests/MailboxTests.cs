using AspNetMailbox;
using FluentAssertions;
using MimeKit;
using Xunit;

namespace AspNetDebugDashboard.Tests;

public class MailboxTests : IDisposable
{
    private readonly string _db = Path.Combine(Path.GetTempPath(), $"mail-test-{Guid.NewGuid():N}.db");

    private static MimeMessage Sample(string subject)
    {
        var bb = new BodyBuilder { HtmlBody = "<p>hello</p>", TextBody = "hello" };
        bb.Attachments.Add("note.txt", "data"u8.ToArray(), new ContentType("text", "plain"));
        var m = new MimeMessage { Subject = subject };
        m.From.Add(MailboxAddress.Parse("sender@shop.test"));
        m.To.Add(MailboxAddress.Parse("buyer@example.com"));
        m.Body = bb.ToMessageBody();
        return m;
    }

    [Fact]
    public void Map_captures_headers_bodies_and_attachments()
    {
        var mapped = MailboxMapper.Map(Sample("Welcome"), size: 512);

        mapped.Subject.Should().Be("Welcome");
        mapped.From.Should().Contain("sender@shop.test");
        mapped.To.Should().ContainSingle().Which.Should().Contain("buyer@example.com");
        mapped.HtmlBody.Should().Contain("hello");
        mapped.Size.Should().Be(512);
        mapped.Attachments.Should().ContainSingle(a => a.FileName == "note.txt" && a.Content.Length > 0);
    }

    [Fact]
    public void Store_persists_lists_searches_and_clears()
    {
        using var store = new LiteDbMailboxStore(new MailboxOptions { DatabasePath = _db });
        var msg = MailboxMapper.Map(Sample("Receipt"), 100);
        store.Save(msg);

        store.Count(null).Should().Be(1);
        store.Get(msg.Id)!.Subject.Should().Be("Receipt");
        store.List("receipt", 0, 50).Should().ContainSingle();   // case-insensitive subject search
        store.List("nope", 0, 50).Should().BeEmpty();

        store.Clear();
        store.Count(null).Should().Be(0);
    }

    [Fact]
    public void Store_trims_oldest_beyond_MaxMessages()
    {
        using var store = new LiteDbMailboxStore(new MailboxOptions { DatabasePath = _db, MaxMessages = 2 });
        for (var i = 0; i < 4; i++) store.Save(MailboxMapper.Map(Sample($"m{i}"), 10));
        store.Count(null).Should().Be(2);
    }

    public void Dispose()
    {
        if (File.Exists(_db)) File.Delete(_db);
    }
}
