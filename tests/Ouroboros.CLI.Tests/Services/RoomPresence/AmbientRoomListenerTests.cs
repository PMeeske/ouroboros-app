using Ouroboros.CLI.Services.RoomPresence;

namespace Ouroboros.Tests.CLI.Services.RoomPresence;

[Trait("Category", "Unit")]
public class AmbientRoomListenerTests
{
    [Fact]
    public void AmbientRoomListener_ClassExists()
    {
        typeof(AmbientRoomListener).Should().NotBeNull();
    }

    [Fact]
    public void AmbientRoomListener_IsSealed()
    {
        typeof(AmbientRoomListener).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void ImplementsIAsyncDisposable()
    {
        typeof(AmbientRoomListener).Should().Implement<IAsyncDisposable>();
    }

    [Fact]
    public void HasStartAsyncMethod()
    {
        var method = typeof(AmbientRoomListener).GetMethod("StartAsync");
        method.Should().NotBeNull();
    }
}
