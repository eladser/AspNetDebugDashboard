using System.Collections.Concurrent;
using System.Threading.Channels;

namespace AspNetDebugDashboard.Core.Services;

// Lets the SSE endpoint (added later) know when new entries land, without storage
// knowing anything about SSE. Each subscriber gets its own bounded channel so a slow
// reader drops old events instead of blocking writers.
public class DebugEventBus
{
    private readonly ConcurrentDictionary<Channel<string>, byte> _subscribers = new();

    internal int SubscriberCount => _subscribers.Count;

    public Channel<string> Subscribe()
    {
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        _subscribers.TryAdd(channel, 0);
        return channel;
    }

    public void Unsubscribe(Channel<string> channel)
    {
        _subscribers.TryRemove(channel, out _);
    }

    public void Publish(string type)
    {
        foreach (var channel in _subscribers.Keys)
        {
            channel.Writer.TryWrite(type);
        }
    }
}
