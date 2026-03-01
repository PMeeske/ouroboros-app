using Ouroboros.CLI.Commands;

namespace Ouroboros.Tests.CLI.Commands.Voice;

[Trait("Category", "Unit")]
public class VoiceModeConfigTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var config = new VoiceModeConfig();

        config.Persona.Should().Be("Iaret");
        config.VoiceOnly.Should().BeFalse();
        config.LocalTts.Should().BeFalse();
        config.VoiceLoop.Should().BeTrue();
        config.DisableStt.Should().BeFalse();
        config.Model.Should().Be("llama3");
        config.Endpoint.Should().Be("http://localhost:11434");
        config.EmbedModel.Should().Be("nomic-embed-text");
        config.QdrantEndpoint.Should().Be("http://localhost:6334");
        config.Culture.Should().BeNull();
    }

    [Fact]
    public void WithCustomValues_SetsAllProperties()
    {
        var config = new VoiceModeConfig(
            Persona: "TestPersona",
            VoiceOnly: true,
            LocalTts: true,
            VoiceLoop: false,
            DisableStt: true,
            Model: "gpt-4",
            Endpoint: "https://api.openai.com",
            EmbedModel: "embed-v3",
            QdrantEndpoint: "http://qdrant:6334",
            Culture: "de-DE");

        config.Persona.Should().Be("TestPersona");
        config.VoiceOnly.Should().BeTrue();
        config.LocalTts.Should().BeTrue();
        config.VoiceLoop.Should().BeFalse();
        config.DisableStt.Should().BeTrue();
        config.Model.Should().Be("gpt-4");
        config.Endpoint.Should().Be("https://api.openai.com");
        config.Culture.Should().Be("de-DE");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var c1 = new VoiceModeConfig();
        var c2 = new VoiceModeConfig();

        c1.Should().Be(c2);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var config = new VoiceModeConfig();
        var modified = config with { Persona = "Phoenix" };

        modified.Persona.Should().Be("Phoenix");
        config.Persona.Should().Be("Iaret");
    }
}
