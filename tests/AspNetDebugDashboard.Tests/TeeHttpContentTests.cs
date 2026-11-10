using System.Text;
using AspNetDebugDashboard.Capture;
using FluentAssertions;
using Xunit;

namespace AspNetDebugDashboard.Tests;

public class TeeHttpContentTests
{
    [Fact]
    public async Task Known_content_length_is_preserved_when_bodies_capture_is_on()
    {
        var body = "hello world";
        var inner = new StringContent(body, Encoding.UTF8);
        var expectedLength = inner.Headers.ContentLength;

        string? captured = null;
        var tee = new TeeHttpContent(inner, maxBytes: 1024, onComplete: b => captured = b);

        tee.Headers.ContentLength.Should().Be(expectedLength);

        using var dest = new MemoryStream();
        await tee.CopyToAsync(dest);

        Encoding.UTF8.GetString(dest.ToArray()).Should().Be(body);
        captured.Should().Be(body);
    }

    [Fact]
    public void Content_length_not_yet_materialized_on_inner_is_computed_lazily()
    {
        // StreamContent doesn't set Content-Length up front, so the ctor's header copy
        // loop won't see it either - TryComputeLength has to fall through to the inner stream.
        var inner = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes("abc")));

        var tee = new TeeHttpContent(inner, maxBytes: 1024, onComplete: _ => { });

        tee.Headers.ContentLength.Should().Be(3);
    }
}
