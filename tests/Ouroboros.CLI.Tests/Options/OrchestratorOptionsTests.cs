using Ouroboros.CLI.Options;
using Ouroboros.Options;

namespace Ouroboros.Tests.CLI.Options;

[Trait("Category", "Unit")]
public class OrchestratorOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new OrchestratorOptions { Goal = "test" };

        options.Voice.Should().BeFalse();
        options.Persona.Should().Be("Iaret");
        options.EmbedModel.Should().Be("nomic-embed-text");
        options.QdrantEndpoint.Should().Be("http://localhost:6334");
        options.Preset.Should().BeNull();
        options.Goal.Should().Be("test");
        options.Culture.Should().BeNull();
        options.Model.Should().Be("deepseek-v3.1:671b-cloud");
        options.CoderModel.Should().BeNull();
        options.ReasonModel.Should().BeNull();
        options.Embed.Should().Be("nomic-embed-text");
        options.Temperature.Should().Be(0.7);
        options.MaxTokens.Should().Be(2048);
        options.TimeoutSeconds.Should().Be(60);
        options.ShowMetrics.Should().BeTrue();
        options.Debug.Should().BeFalse();
        options.Endpoint.Should().BeNull();
        options.ApiKey.Should().BeNull();
        options.EndpointType.Should().BeNull();
        options.VoiceOnly.Should().BeFalse();
        options.LocalTts.Should().BeFalse();
        options.VoiceLoop.Should().BeFalse();
    }

    [Fact]
    public void ImplementsIVoiceOptions()
    {
        var options = new OrchestratorOptions { Goal = "test" };
        options.Should().BeAssignableTo<IVoiceOptions>();
    }

    [Fact]
    public void IVoiceOptions_Endpoint_DefaultsToLocalhost()
    {
        var options = new OrchestratorOptions { Goal = "test" };
        IVoiceOptions voiceOptions = options;

        voiceOptions.Endpoint.Should().Be("http://localhost:11434");
    }

    [Fact]
    public void IVoiceOptions_Endpoint_ReturnsSetValue()
    {
        var options = new OrchestratorOptions
        {
            Goal = "test",
            Endpoint = "http://custom:1234"
        };
        IVoiceOptions voiceOptions = options;

        voiceOptions.Endpoint.Should().Be("http://custom:1234");
    }
}
