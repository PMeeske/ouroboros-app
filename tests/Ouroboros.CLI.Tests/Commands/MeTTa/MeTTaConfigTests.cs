using Ouroboros.CLI.Commands;

namespace Ouroboros.Tests.CLI.Commands.MeTTa;

[Trait("Category", "Unit")]
public class MeTTaConfigTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var config = new MeTTaConfig();

        config.Goal.Should().BeEmpty();
        config.Culture.Should().BeNull();
        config.Model.Should().Be("deepseek-v3.1:671b-cloud");
        config.Temperature.Should().Be(0.7);
        config.MaxTokens.Should().Be(2048);
        config.TimeoutSeconds.Should().Be(60);
        config.Endpoint.Should().BeNull();
        config.ApiKey.Should().BeNull();
        config.EndpointType.Should().BeNull();
        config.Debug.Should().BeFalse();
        config.Embed.Should().Be("nomic-embed-text");
        config.EmbedModel.Should().Be("nomic-embed-text");
        config.QdrantEndpoint.Should().Be("http://localhost:6334");
        config.PlanOnly.Should().BeFalse();
        config.ShowMetrics.Should().BeTrue();
        config.Interactive.Should().BeFalse();
        config.Persona.Should().Be("Iaret");
        config.Voice.Should().BeFalse();
        config.VoiceOnly.Should().BeFalse();
        config.LocalTts.Should().BeFalse();
        config.VoiceLoop.Should().BeFalse();
    }

    [Fact]
    public void WithCustomValues_SetsCorrectly()
    {
        var config = new MeTTaConfig(
            Goal: "test goal",
            Model: "gpt-4",
            Debug: true,
            Interactive: true);

        config.Goal.Should().Be("test goal");
        config.Model.Should().Be("gpt-4");
        config.Debug.Should().BeTrue();
        config.Interactive.Should().BeTrue();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var c1 = new MeTTaConfig();
        var c2 = new MeTTaConfig();

        c1.Should().Be(c2);
    }
}
