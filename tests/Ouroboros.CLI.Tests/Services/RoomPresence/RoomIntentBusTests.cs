using Ouroboros.CLI.Services.RoomPresence;

namespace Ouroboros.Tests.CLI.Services.RoomPresence;

[Trait("Category", "Unit")]
public class RoomIntentBusTests : IDisposable
{
    public RoomIntentBusTests()
    {
        // Reset before each test to avoid interference
        RoomIntentBus.Reset();
    }

    public void Dispose()
    {
        RoomIntentBus.Reset();
    }

    [Fact]
    public void Reset_ClearsAllEventHandlers()
    {
        bool called = false;
        RoomIntentBus.OnIaretInterjected += (_, _) => called = true;
        RoomIntentBus.Reset();

        // After reset, the handler should not fire
        // (We can't directly fire internal methods, but reset should clear)
        RoomIntentBus.OnIaretInterjected.Should().BeNull();
    }

    [Fact]
    public void Reset_ClearsUserAddressedIaret()
    {
        RoomIntentBus.OnUserAddressedIaret += (_, _) => { };
        RoomIntentBus.Reset();

        RoomIntentBus.OnUserAddressedIaret.Should().BeNull();
    }

    [Fact]
    public void Reset_ClearsSpeakerIdentified()
    {
        RoomIntentBus.OnSpeakerIdentified += (_, _) => { };
        RoomIntentBus.Reset();

        RoomIntentBus.OnSpeakerIdentified.Should().BeNull();
    }

    [Fact]
    public void OnIaretInterjected_CanSubscribeAndUnsubscribe()
    {
        Action<string, string> handler = (_, _) => { };

        RoomIntentBus.OnIaretInterjected += handler;
        RoomIntentBus.OnIaretInterjected.Should().NotBeNull();

        RoomIntentBus.OnIaretInterjected -= handler;
    }

    [Fact]
    public void OnUserAddressedIaret_CanSubscribeAndUnsubscribe()
    {
        Action<string, string> handler = (_, _) => { };

        RoomIntentBus.OnUserAddressedIaret += handler;
        RoomIntentBus.OnUserAddressedIaret.Should().NotBeNull();

        RoomIntentBus.OnUserAddressedIaret -= handler;
    }

    [Fact]
    public void OnSpeakerIdentified_CanSubscribeAndUnsubscribe()
    {
        Action<string, bool> handler = (_, _) => { };

        RoomIntentBus.OnSpeakerIdentified += handler;
        RoomIntentBus.OnSpeakerIdentified.Should().NotBeNull();

        RoomIntentBus.OnSpeakerIdentified -= handler;
    }
}
