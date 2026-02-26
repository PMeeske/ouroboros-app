using Ouroboros.CLI.Commands.Options;

namespace Ouroboros.Tests.CLI.Commands.Options;

[Trait("Category", "Unit")]
public class VoiceCommandOptionsTests
{
    [Fact]
    public void VoiceOption_HasDescription()
    {
        var options = new VoiceCommandOptions();
        options.VoiceOption.Description.Should().Contain("voice persona");
    }

    [Fact]
    public void PersonaOption_HasDescription()
    {
        var options = new VoiceCommandOptions();
        options.PersonaOption.Description.Should().Contain("Persona name");
    }

    [Fact]
    public void EmbedModelOption_HasDescription()
    {
        var options = new VoiceCommandOptions();
        options.EmbedModelOption.Description.Should().Contain("Embedding model");
    }

    [Fact]
    public void QdrantEndpointOption_HasDescription()
    {
        var options = new VoiceCommandOptions();
        options.QdrantEndpointOption.Description.Should().Contain("Qdrant");
    }

    [Fact]
    public void VoiceOnlyOption_HasDescription()
    {
        var options = new VoiceCommandOptions();
        options.VoiceOnlyOption.Description.Should().Contain("Voice-only");
    }

    [Fact]
    public void LocalTtsOption_HasDescription()
    {
        var options = new VoiceCommandOptions();
        options.LocalTtsOption.Description.Should().Contain("local TTS");
    }

    [Fact]
    public void VoiceLoopOption_HasDescription()
    {
        var options = new VoiceCommandOptions();
        options.VoiceLoopOption.Description.Should().Contain("voice conversation");
    }
}
