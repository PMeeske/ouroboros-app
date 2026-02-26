using Ouroboros.CLI.Commands;

namespace Ouroboros.Tests.CLI.Commands.Immersive;

[Trait("Category", "Unit")]
public class ImmersiveConfigTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var config = new ImmersiveConfig();

        config.Persona.Should().Be("Iaret");
        config.Model.Should().Be("deepseek-v3.1:671b-cloud");
        config.Endpoint.Should().Be("http://localhost:11434");
        config.EmbedModel.Should().Be("nomic-embed-text");
        config.QdrantEndpoint.Should().Be("http://localhost:6334");
        config.Voice.Should().BeFalse();
        config.VoiceOnly.Should().BeFalse();
        config.LocalTts.Should().BeFalse();
        config.AzureTts.Should().BeTrue();
        config.AzureSpeechKey.Should().BeNull();
        config.AzureSpeechRegion.Should().Be("eastus");
        config.TtsVoice.Should().Be("en-US-AvaMultilingualNeural");
        config.Avatar.Should().BeTrue();
        config.AvatarCloud.Should().BeFalse();
        config.AvatarPort.Should().Be(9471);
        config.RoomMode.Should().BeFalse();
        config.EnableOpenClaw.Should().BeTrue();
        config.OpenClawGateway.Should().BeNull();
        config.OpenClawToken.Should().BeNull();
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var config = new ImmersiveConfig();
        var modified = config with { Voice = true, RoomMode = true };

        modified.Voice.Should().BeTrue();
        modified.RoomMode.Should().BeTrue();
        config.Voice.Should().BeFalse();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var c1 = new ImmersiveConfig();
        var c2 = new ImmersiveConfig();

        c1.Should().Be(c2);
    }
}
