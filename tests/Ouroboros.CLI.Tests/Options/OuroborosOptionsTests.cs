using Ouroboros.CLI.Options;
using Ouroboros.Options;

namespace Ouroboros.Tests.CLI.Options;

[Trait("Category", "Unit")]
public class OuroborosOptionsTests
{
    [Fact]
    public void DefaultValues_VoiceAndInteraction()
    {
        var options = new OuroborosOptions();

        options.Voice.Should().BeTrue();
        options.TextOnly.Should().BeFalse();
        options.VoiceOnly.Should().BeFalse();
        options.LocalTts.Should().BeFalse();
        options.AzureTts.Should().BeTrue();
        options.AzureSpeechKey.Should().BeNull();
        options.AzureSpeechRegion.Should().Be("eastus");
        options.TtsVoice.Should().Be("en-US-AvaMultilingualNeural");
        options.VoiceChannel.Should().BeFalse();
        options.VoiceV2.Should().BeFalse();
        options.Listen.Should().BeFalse();
        options.VoiceLoop.Should().BeTrue();
        options.Persona.Should().Be("Iaret");
    }

    [Fact]
    public void DefaultValues_LlmConfiguration()
    {
        var options = new OuroborosOptions();

        options.Model.Should().Be("deepseek-v3.1:671b-cloud");
        options.Culture.Should().BeNull();
        options.Endpoint.Should().Be("http://localhost:11434");
        options.ApiKey.Should().BeNull();
        options.EndpointType.Should().BeNull();
        options.Temperature.Should().Be(0.7);
        options.MaxTokens.Should().Be(2048);
        options.TimeoutSeconds.Should().Be(120);
    }

    [Fact]
    public void DefaultValues_EmbeddingsAndMemory()
    {
        var options = new OuroborosOptions();

        options.EmbedModel.Should().Be("nomic-embed-text");
        options.EmbedEndpoint.Should().Be("http://localhost:11434");
        options.QdrantEndpoint.Should().Be("http://localhost:6334");
    }

    [Fact]
    public void DefaultValues_FeatureToggles()
    {
        var options = new OuroborosOptions();

        options.NoSkills.Should().BeFalse();
        options.NoMeTTa.Should().BeFalse();
        options.NoTools.Should().BeFalse();
        options.NoPersonality.Should().BeFalse();
        options.NoMind.Should().BeFalse();
        options.NoBrowser.Should().BeFalse();
    }

    [Fact]
    public void DefaultValues_AutonomousMode()
    {
        var options = new OuroborosOptions();

        options.Push.Should().BeFalse();
        options.PushVoice.Should().BeFalse();
        options.Yolo.Should().BeFalse();
        options.AutoApprove.Should().Be("");
        options.IntentionInterval.Should().Be(45);
        options.DiscoveryInterval.Should().Be(90);
    }

    [Fact]
    public void DefaultValues_Governance()
    {
        var options = new OuroborosOptions();

        options.EnableSelfModification.Should().BeFalse();
        options.RiskLevel.Should().Be("Medium");
        options.AutoApproveLow.Should().BeTrue();
    }

    [Fact]
    public void DefaultValues_InitialTask()
    {
        var options = new OuroborosOptions();

        options.Goal.Should().BeNull();
        options.Question.Should().BeNull();
        options.Dsl.Should().BeNull();
    }

    [Fact]
    public void DefaultValues_MultiModel()
    {
        var options = new OuroborosOptions();

        options.CoderModel.Should().BeNull();
        options.ReasonModel.Should().BeNull();
        options.SummarizeModel.Should().BeNull();
        options.VisionModel.Should().BeNull();
    }

    [Fact]
    public void DefaultValues_AgentBehavior()
    {
        var options = new OuroborosOptions();

        options.AgentMaxSteps.Should().Be(10);
        options.ThinkingInterval.Should().Be(30);
    }

    [Fact]
    public void DefaultValues_PipingAndBatch()
    {
        var options = new OuroborosOptions();

        options.Pipe.Should().BeFalse();
        options.BatchFile.Should().BeNull();
        options.JsonOutput.Should().BeFalse();
        options.NoGreeting.Should().BeFalse();
        options.ExitOnError.Should().BeFalse();
        options.Exec.Should().BeNull();
    }

    [Fact]
    public void DefaultValues_Avatar()
    {
        var options = new OuroborosOptions();

        options.Avatar.Should().BeTrue();
        options.AvatarPort.Should().Be(0);
        options.RoomMode.Should().BeFalse();
    }

    [Fact]
    public void DefaultValues_DebugAndOutput()
    {
        var options = new OuroborosOptions();

        options.Debug.Should().BeFalse();
        options.Trace.Should().BeFalse();
        options.ShowMetrics.Should().BeFalse();
        options.Stream.Should().BeTrue();
    }

    [Fact]
    public void DefaultValues_CostTracking()
    {
        var options = new OuroborosOptions();

        options.ShowCosts.Should().BeFalse();
        options.CostAware.Should().BeFalse();
        options.CostSummary.Should().BeTrue();
    }

    [Fact]
    public void DefaultValues_CollectiveMind()
    {
        var options = new OuroborosOptions();

        options.CollectiveMode.Should().BeFalse();
        options.CollectivePreset.Should().BeNull();
        options.CollectiveThinkingMode.Should().Be("adaptive");
        options.CollectiveProviders.Should().BeNull();
        options.Failover.Should().BeTrue();
    }

    [Fact]
    public void DefaultValues_Election()
    {
        var options = new OuroborosOptions();

        options.ElectionStrategy.Should().Be("weighted");
        options.MasterModel.Should().BeNull();
        options.EvaluationCriteria.Should().Be("default");
        options.ShowElection.Should().BeFalse();
        options.ShowOptimization.Should().BeFalse();
    }

    [Fact]
    public void ImplementsIVoiceOptions()
    {
        var options = new OuroborosOptions();
        options.Should().BeAssignableTo<IVoiceOptions>();
    }
}
