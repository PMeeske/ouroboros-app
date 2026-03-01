// <copyright file="OpenClawEventBusTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using FluentAssertions;
using Ouroboros.Application.OpenClaw;
using Ouroboros.Application.OpenClaw.PcNode;
using Xunit;

namespace Ouroboros.Tests.OpenClaw;

[Trait("Category", "Unit")]
[Trait("Area", "OpenClaw")]
public class OpenClawEventBusTests
{
    private static OpenClawEvent MakeEvent(string type, string? channel = null)
    {
        var payload = channel != null
            ? JsonSerializer.SerializeToElement(new { channel })
            : JsonSerializer.SerializeToElement(new { });
        return new OpenClawEvent(type, payload, DateTime.UtcNow);
    }

    [Fact]
    public void NewBus_IsEmpty()
    {
        var bus = new OpenClawEventBus();
        bus.Count.Should().Be(0);
        bus.GetRecent().Should().BeEmpty();
    }

    [Fact]
    public void Publish_IncreasesCount()
    {
        var bus = new OpenClawEventBus();
        bus.Publish(MakeEvent("test"));
        bus.Count.Should().Be(1);
    }

    [Fact]
    public void GetRecent_ReturnsEventsNewestFirst()
    {
        var bus = new OpenClawEventBus();
        bus.Publish(MakeEvent("first"));
        bus.Publish(MakeEvent("second"));
        bus.Publish(MakeEvent("third"));

        var events = bus.GetRecent(limit: 3);
        events.Should().HaveCount(3);
        events[0].EventType.Should().Be("third");
        events[1].EventType.Should().Be("second");
        events[2].EventType.Should().Be("first");
    }

    [Fact]
    public void GetRecent_RespectsLimit()
    {
        var bus = new OpenClawEventBus();
        for (int i = 0; i < 10; i++)
            bus.Publish(MakeEvent($"event-{i}"));

        var events = bus.GetRecent(limit: 3);
        events.Should().HaveCount(3);
    }

    [Fact]
    public void GetRecent_FiltersbyType()
    {
        var bus = new OpenClawEventBus();
        bus.Publish(MakeEvent("message.received"));
        bus.Publish(MakeEvent("node.connected"));
        bus.Publish(MakeEvent("message.received"));
        bus.Publish(MakeEvent("health"));

        var messages = bus.GetRecent(eventType: "message.received");
        messages.Should().HaveCount(2);
        messages.Should().OnlyContain(e => e.EventType == "message.received");
    }

    [Fact]
    public void RingBuffer_OverwritesOldestWhenFull()
    {
        var bus = new OpenClawEventBus(capacity: 3);
        bus.Publish(MakeEvent("a"));
        bus.Publish(MakeEvent("b"));
        bus.Publish(MakeEvent("c"));
        bus.Publish(MakeEvent("d")); // Should overwrite "a"

        bus.Count.Should().Be(3);
        var events = bus.GetRecent(limit: 10);
        events.Should().HaveCount(3);
        events.Select(e => e.EventType).Should().NotContain("a");
        events.Select(e => e.EventType).Should().Contain("d");
    }

    [Fact]
    public void GetRecentMessages_FiltersByMessageType()
    {
        var bus = new OpenClawEventBus();
        bus.Publish(MakeEvent("message.received", "whatsapp"));
        bus.Publish(MakeEvent("node.connected"));
        bus.Publish(MakeEvent("chat", "telegram"));
        bus.Publish(MakeEvent("health"));

        var messages = bus.GetRecentMessages();
        messages.Should().HaveCount(2);
    }

    [Fact]
    public void GetRecentMessages_FiltersByChannel()
    {
        var bus = new OpenClawEventBus();
        bus.Publish(MakeEvent("message.received", "whatsapp"));
        bus.Publish(MakeEvent("message.received", "telegram"));
        bus.Publish(MakeEvent("chat", "whatsapp"));

        var messages = bus.GetRecentMessages(channel: "whatsapp");
        messages.Should().HaveCount(2);
    }

    [Fact]
    public async Task PollAsync_ReturnsNewEvents()
    {
        var bus = new OpenClawEventBus();

        // Start polling in background
        var pollTask = bus.PollAsync(timeout: TimeSpan.FromSeconds(5));

        // Publish event after a short delay
        await Task.Delay(100);
        bus.Publish(MakeEvent("new.event"));

        var events = await pollTask;
        events.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PollAsync_TimesOutGracefully()
    {
        var bus = new OpenClawEventBus();

        // Pre-seed to establish count baseline
        bus.Publish(MakeEvent("old"));

        var events = await bus.PollAsync(timeout: TimeSpan.FromMilliseconds(100));
        // May or may not return events depending on timing, but shouldn't throw
        events.Should().NotBeNull();
    }

    [Fact]
    public void ThreadSafety_ConcurrentPublish()
    {
        var bus = new OpenClawEventBus(capacity: 100);
        var tasks = Enumerable.Range(0, 10).Select(i =>
            Task.Run(() =>
            {
                for (int j = 0; j < 50; j++)
                    bus.Publish(MakeEvent($"event-{i}-{j}"));
            }));

        Task.WaitAll(tasks.ToArray());

        // Should have at most capacity events, no crashes
        bus.Count.Should().BeLessThanOrEqualTo(100);
        bus.Count.Should().BeGreaterThan(0);
    }
}
