using Ouroboros.CLI.Services.RoomPresence;

namespace Ouroboros.Tests.CLI.Services.RoomPresence;

[Trait("Category", "Unit")]
public class GestureDetectorTests
{
    [Fact]
    public void GestureDetector_IsSealed()
    {
        typeof(GestureDetector).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void ImplementsIAsyncDisposable()
    {
        var detector = new GestureDetector();
        detector.Should().BeAssignableTo<IAsyncDisposable>();
    }

    [Fact]
    public void OnGestureDetected_InitiallyNull()
    {
        var detector = new GestureDetector();

        // The event should be subscribable
        var eventInfo = typeof(GestureDetector).GetEvent("OnGestureDetected");
        eventInfo.Should().NotBeNull();
    }

    [Fact]
    public async Task DisposeAsync_DoesNotThrow()
    {
        var detector = new GestureDetector();

        var action = async () => await detector.DisposeAsync();

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        var detector = new GestureDetector();

        await detector.DisposeAsync();

        var action = async () => await detector.DisposeAsync();

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public void StartAsync_MethodExists()
    {
        var method = typeof(GestureDetector).GetMethod("StartAsync");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task));
    }
}
