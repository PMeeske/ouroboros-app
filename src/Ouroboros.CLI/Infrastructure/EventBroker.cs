namespace Ouroboros.CLI.Infrastructure;

using System.Threading.Channels;
using System.Collections.Concurrent;

/// <summary>
/// Non-blocking publish/subscribe broker inspired by Crush's agent↔UI decoupling.
/// Publishers never block — slow subscribers silently drop messages rather than
/// stalling the agent loop.
/// </summary>
/// <typeparam name="T">Event type published on this broker.</typeparam>
public sealed class EventBroker<T> : IDisposable
{
    private readonly ConcurrentBag<Channel<T>> _channels = [];
    private readonly int _capacity;
    private bool _disposed;

    /// <param name="capacity">Per-subscriber buffer size before drops occur.</param>
    public EventBroker(int capacity = 64)
    {
        _capacity = capacity;
    }

    /// <summary>
    /// Returns a reader that receives published events.
    /// The channel is automatically completed when <paramref name="ct"/> is cancelled.
    /// </summary>
    public ChannelReader<T> Subscribe(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var ch = Channel.CreateBounded<T>(new BoundedChannelOptions(_capacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,   // drop rather than block publisher
            SingleWriter = false,
            SingleReader = true,
        });

        _channels.Add(ch);
        ct.Register(() => ch.Writer.TryComplete());
        return ch.Reader;
    }

    /// <summary>
    /// Publishes an event to all active subscribers.
    /// Fire-and-forget; never throws even if a channel is full or complete.
    /// </summary>
    public void Publish(T evt)
    {
        if (_disposed) return;
        foreach (var ch in _channels)
            ch.Writer.TryWrite(evt);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var ch in _channels)
            ch.Writer.TryComplete();
    }
}
