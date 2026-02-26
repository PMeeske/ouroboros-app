using Ouroboros.CLI.Commands;

namespace Ouroboros.Tests.CLI.Commands.Room;

[Trait("Category", "Unit")]
public class RoomConfigTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var config = new RoomConfig();

        config.Persona.Should().Be("Iaret");
        config.Model.Should().Be("deepseek-v3.1:671b-cloud");
        config.Endpoint.Should().Be("http://localhost:11434");
        config.LocalTts.Should().BeFalse();
        config.Avatar.Should().BeTrue();
        config.Quiet.Should().BeFalse();
        config.CooldownSeconds.Should().Be(20);
        config.MaxInterjections.Should().Be(8);
        config.PhiThreshold.Should().Be(0.05);
        config.Proactive.Should().BeTrue();
        config.IdleDelaySeconds.Should().Be(120);
        config.EnableCamera.Should().BeFalse();
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var config = new RoomConfig();
        var modified = config with { Quiet = true, CooldownSeconds = 10 };

        modified.Quiet.Should().BeTrue();
        modified.CooldownSeconds.Should().Be(10);
        config.Quiet.Should().BeFalse();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var c1 = new RoomConfig();
        var c2 = new RoomConfig();

        c1.Should().Be(c2);
    }
}
