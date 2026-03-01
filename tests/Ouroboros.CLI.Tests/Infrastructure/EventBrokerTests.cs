using Ouroboros.CLI.Infrastructure;

namespace Ouroboros.Tests.CLI.Infrastructure;

[Trait("Category", "Unit")]
public class EventBrokerTests
{
    [Fact]
    public void Constructor_DefaultCapacity_DoesNotThrow()
    {
        var act = () => new EventBroker<string>();

        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_CustomCapacity_DoesNotThrow()
    {
        var act = () => new EventBroker<string>(128);

        act.Should().NotThrow();
    }

    [Fact]
    public void Subscribe_ReturnsChannelReader()
    {
        using var broker = new EventBroker<string>();

        var reader = broker.Subscribe();

        reader.Should().NotBeNull();
    }

    [Fact]
    public async Task Publish_WithSubscriber_SubscriberReceivesEvent()
    {
        using var broker = new EventBroker<string>();
        using var cts = new CancellationTokenSource();
        var reader = broker.Subscribe(cts.Token);

        broker.Publish("test-event");

        var received = await reader.ReadAsync(cts.Token);
        received.Should().Be("test-event");
    }

    [Fact]
    public async Task Publish_MultipleEvents_AllReceived()
    {
        using var broker = new EventBroker<int>();
        using var cts = new CancellationTokenSource();
        var reader = broker.Subscribe(cts.Token);

        broker.Publish(1);
        broker.Publish(2);
        broker.Publish(3);

        var val1 = await reader.ReadAsync(cts.Token);
        var val2 = await reader.ReadAsync(cts.Token);
        var val3 = await reader.ReadAsync(cts.Token);

        val1.Should().Be(1);
        val2.Should().Be(2);
        val3.Should().Be(3);
    }

    [Fact]
    public async Task Publish_MultipleSubscribers_AllReceiveEvent()
    {
        using var broker = new EventBroker<string>();
        using var cts = new CancellationTokenSource();
        var reader1 = broker.Subscribe(cts.Token);
        var reader2 = broker.Subscribe(cts.Token);

        broker.Publish("broadcast");

        var r1 = await reader1.ReadAsync(cts.Token);
        var r2 = await reader2.ReadAsync(cts.Token);

        r1.Should().Be("broadcast");
        r2.Should().Be("broadcast");
    }

    [Fact]
    public void Publish_AfterDispose_DoesNotThrow()
    {
        var broker = new EventBroker<string>();
        broker.Dispose();

        var act = () => broker.Publish("event");

        act.Should().NotThrow();
    }

    [Fact]
    public void Subscribe_AfterDispose_ThrowsObjectDisposed()
    {
        var broker = new EventBroker<string>();
        broker.Dispose();

        var act = () => broker.Subscribe();

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_MultipleTimes_DoesNotThrow()
    {
        var broker = new EventBroker<string>();

        var act = () =>
        {
            broker.Dispose();
            broker.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CompletesSubscriberChannels()
    {
        var broker = new EventBroker<string>();
        var reader = broker.Subscribe();

        broker.Dispose();

        // Completion should be signaled eventually
        reader.Completion.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task Subscribe_WithCancellationToken_CompletesOnCancel()
    {
        using var broker = new EventBroker<string>();
        using var cts = new CancellationTokenSource();

        var reader = broker.Subscribe(cts.Token);
        cts.Cancel();

        // After cancellation, the channel should be completed
        await Task.Delay(50); // Allow time for cancellation callback
        reader.Completion.IsCompleted.Should().BeTrue();
    }
}
