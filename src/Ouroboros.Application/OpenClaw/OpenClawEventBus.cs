// <copyright file="OpenClawEventBus.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.OpenClaw.PcNode;

namespace Ouroboros.Application.OpenClaw;

/// <summary>
/// Bounded ring buffer for OpenClaw events. Stores the most recent events
/// and supports typed queries and blocking poll operations.
/// Thread-safe for concurrent producers and consumers.
/// </summary>
public sealed class OpenClawEventBus
{
    private readonly object _lock = new();
    private readonly OpenClawEvent[] _buffer;
    private int _head;
    private int _count;
    private readonly SemaphoreSlim _newEventSignal = new(0, int.MaxValue);

    /// <summary>Maximum events retained in the ring buffer.</summary>
    public int Capacity { get; }

    public OpenClawEventBus(int capacity = 200)
    {
        Capacity = capacity;
        _buffer = new OpenClawEvent[capacity];
    }

    /// <summary>
    /// Publishes an event to the bus.
    /// </summary>
    public void Publish(OpenClawEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        lock (_lock)
        {
            var index = (_head + _count) % Capacity;
            if (_count == Capacity)
            {
                // Overwrite oldest
                _buffer[_head] = evt;
                _head = (_head + 1) % Capacity;
            }
            else
            {
                _buffer[index] = evt;
                _count++;
            }
        }

        // Signal waiters (best-effort, don't overflow the semaphore)
        try { _newEventSignal.Release(); }
        catch (SemaphoreFullException) { }
    }

    /// <summary>
    /// Gets recent events, optionally filtered by type.
    /// </summary>
    public IReadOnlyList<OpenClawEvent> GetRecent(
        string? eventType = null,
        int limit = 20)
    {
        lock (_lock)
        {
            var events = new List<OpenClawEvent>(Math.Min(limit, _count));
            // Iterate from newest to oldest
            for (int i = _count - 1; i >= 0 && events.Count < limit; i--)
            {
                var idx = (_head + i) % Capacity;
                var evt = _buffer[idx];
                if (eventType == null || evt.EventType.Equals(eventType, StringComparison.OrdinalIgnoreCase))
                    events.Add(evt);
            }
            return events;
        }
    }

    /// <summary>
    /// Gets recent messages (events of type "message.received" or "chat").
    /// </summary>
    public IReadOnlyList<OpenClawEvent> GetRecentMessages(
        string? channel = null,
        int limit = 20)
    {
        lock (_lock)
        {
            var events = new List<OpenClawEvent>(Math.Min(limit, _count));
            for (int i = _count - 1; i >= 0 && events.Count < limit; i--)
            {
                var idx = (_head + i) % Capacity;
                var evt = _buffer[idx];
                if (evt.EventType is not ("message.received" or "chat"))
                    continue;
                if (channel != null && !MatchesChannel(evt.Payload, channel))
                    continue;
                events.Add(evt);
            }
            return events;
        }
    }

    /// <summary>
    /// Waits for new events with a timeout. Returns events that arrive during the wait.
    /// </summary>
    public async Task<IReadOnlyList<OpenClawEvent>> PollAsync(
        string? eventType = null,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        var deadline = timeout ?? TimeSpan.FromSeconds(30);

        // Record current count so we can detect new events
        int countBefore;
        lock (_lock) { countBefore = _count; }

        // Wait for new event signal or timeout
        await _newEventSignal.WaitAsync(deadline, ct).ConfigureAwait(false);

        // Return any new events since we started waiting
        lock (_lock)
        {
            var newCount = _count - countBefore;
            if (newCount <= 0)
                return Array.Empty<OpenClawEvent>();

            var events = new List<OpenClawEvent>(newCount);
            for (int i = Math.Max(0, _count - newCount); i < _count; i++)
            {
                var idx = (_head + i) % Capacity;
                var evt = _buffer[idx];
                if (eventType == null || evt.EventType.Equals(eventType, StringComparison.OrdinalIgnoreCase))
                    events.Add(evt);
            }
            return events;
        }
    }

    /// <summary>Total events currently in the buffer.</summary>
    public int Count
    {
        get { lock (_lock) { return _count; } }
    }

    private static bool MatchesChannel(JsonElement payload, string channel)
    {
        if (payload.TryGetProperty("channel", out var ch))
            return ch.GetString()?.Equals(channel, StringComparison.OrdinalIgnoreCase) ?? false;
        if (payload.TryGetProperty("source", out var src))
            return src.GetString()?.Equals(channel, StringComparison.OrdinalIgnoreCase) ?? false;
        return false;
    }
}
