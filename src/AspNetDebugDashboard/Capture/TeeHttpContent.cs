using System.Net;

namespace AspNetDebugDashboard.Capture;

// Wraps a response's HttpContent so the bytes flow to the real caller unchanged while a
// capped copy is buffered on the side. onComplete fires once with whatever was captured
// (up to maxBytes), whether the caller reads to the end, disposes early, or never reads at all.
internal sealed class TeeHttpContent : HttpContent
{
    private readonly HttpContent _inner;
    private readonly int _maxBytes;
    private readonly Action<string> _onComplete;
    private readonly MemoryStream _buffer = new();
    private bool _completed;

    public TeeHttpContent(HttpContent inner, int maxBytes, Action<string> onComplete)
    {
        _inner = inner;
        _maxBytes = maxBytes;
        _onComplete = onComplete;

        foreach (var header in inner.Headers)
        {
            if (header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) continue;
            Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        var source = await _inner.ReadAsStreamAsync().ConfigureAwait(false);
        var buf = new byte[8192];
        int read;
        while ((read = await source.ReadAsync(buf).ConfigureAwait(false)) > 0)
        {
            await stream.WriteAsync(buf.AsMemory(0, read)).ConfigureAwait(false);
            if (_buffer.Length < _maxBytes)
            {
                var take = (int)Math.Min(read, _maxBytes - _buffer.Length);
                _buffer.Write(buf, 0, take);
            }
        }
        Complete();
    }

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }

    protected override void Dispose(bool disposing)
    {
        Complete();
        if (disposing)
        {
            _inner.Dispose();
            _buffer.Dispose();
        }
        base.Dispose(disposing);
    }

    private void Complete()
    {
        if (_completed) return;
        _completed = true;

        var text = System.Text.Encoding.UTF8.GetString(_buffer.ToArray());
        if (_buffer.Length >= _maxBytes) text += "\n...[truncated]";
        _onComplete(text);
    }
}
