using Ouroboros.CLI.Options;
using Ouroboros.Options;

namespace Ouroboros.Tests.CLI.Options;

[Trait("Category", "Unit")]
public class PipelineOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new PipelineOptions { Dsl = "ask 'hello'" };

        options.Voice.Should().BeFalse();
        options.Persona.Should().Be("Iaret");
        options.EmbedModel.Should().Be("nomic-embed-text");
        options.QdrantEndpoint.Should().Be("http://localhost:6334");
        options.Dsl.Should().Be("ask 'hello'");
        options.Culture.Should().BeNull();
        options.Model.Should().Be("deepseek-v3.1:671b-cloud");
        options.Embed.Should().Be("nomic-embed-text");
        options.Source.Should().Be(".");
        options.K.Should().Be(8);
        options.Trace.Should().BeFalse();
        options.Debug.Should().BeFalse();
        options.Temperature.Should().Be(0.7);
        options.MaxTokens.Should().Be(2048);
        options.TimeoutSeconds.Should().Be(60);
        options.Stream.Should().BeFalse();
        options.Router.Should().Be("off");
        options.CoderModel.Should().BeNull();
        options.SummarizeModel.Should().BeNull();
        options.ReasonModel.Should().BeNull();
        options.GeneralModel.Should().BeNull();
        options.Agent.Should().BeFalse();
        options.AgentMode.Should().Be("lc");
        options.AgentMaxSteps.Should().Be(6);
        options.CritiqueIterations.Should().Be(1);
        options.StrictModel.Should().BeFalse();
        options.JsonTools.Should().BeFalse();
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
        var options = new PipelineOptions { Dsl = "test" };
        options.Should().BeAssignableTo<IVoiceOptions>();
    }

    [Fact]
    public void IVoiceOptions_Endpoint_DefaultsToLocalhost()
    {
        var options = new PipelineOptions { Dsl = "test" };
        IVoiceOptions voiceOptions = options;

        voiceOptions.Endpoint.Should().Be("http://localhost:11434");
    }
}
