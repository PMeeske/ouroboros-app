using Ouroboros.CLI.Commands;

namespace Ouroboros.Tests.CLI.Commands.Ouroboros;

[Trait("Category", "Unit")]
public class OuroborosConfigTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var config = new OuroborosConfig();

        config.Persona.Should().Be("Iaret");
        config.Model.Should().Be("deepseek-v3.1:671b-cloud");
        config.Endpoint.Should().Be("http://localhost:11434");
        config.EmbedModel.Should().Be("nomic-embed-text");
        config.EmbedEndpoint.Should().Be("http://localhost:11434");
        config.QdrantEndpoint.Should().Be("http://localhost:6334");
        config.ApiKey.Should().BeNull();
        config.EndpointType.Should().BeNull();
        config.Voice.Should().BeFalse();
        config.VoiceOnly.Should().BeFalse();
        config.LocalTts.Should().BeFalse();
        config.AzureTts.Should().BeFalse();
        config.Debug.Should().BeFalse();
        config.Verbosity.Should().Be(OutputVerbosity.Normal);
        config.Temperature.Should().Be(0.7);
        config.MaxTokens.Should().Be(2048);
        config.Culture.Should().BeNull();
    }

    [Fact]
    public void FeatureToggles_DefaultToTrue()
    {
        var config = new OuroborosConfig();

        config.EnableSkills.Should().BeTrue();
        config.EnableMeTTa.Should().BeTrue();
        config.EnableTools.Should().BeTrue();
        config.EnablePersonality.Should().BeTrue();
        config.EnableMind.Should().BeTrue();
        config.EnableBrowser.Should().BeTrue();
        config.EnableConsciousness.Should().BeTrue();
        config.EnableEmbodiment.Should().BeTrue();
    }

    [Fact]
    public void AutonomousFeatures_DefaultToDisabled()
    {
        var config = new OuroborosConfig();

        config.EnablePush.Should().BeFalse();
        config.YoloMode.Should().BeFalse();
        config.EnableSelfModification.Should().BeFalse();
    }

    [Fact]
    public void WithModification_CreatesModifiedCopy()
    {
        var config = new OuroborosConfig();
        var modified = config with { Debug = true, Model = "gpt-4" };

        modified.Debug.Should().BeTrue();
        modified.Model.Should().Be("gpt-4");
        config.Debug.Should().BeFalse();
        config.Model.Should().Be("deepseek-v3.1:671b-cloud");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var c1 = new OuroborosConfig();
        var c2 = new OuroborosConfig();

        c1.Should().Be(c2);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var c1 = new OuroborosConfig(Debug: true);
        var c2 = new OuroborosConfig(Debug: false);

        c1.Should().NotBe(c2);
    }

    [Fact]
    public void CollectiveMode_DefaultsToDisabled()
    {
        var config = new OuroborosConfig();

        config.CollectiveMode.Should().BeFalse();
        config.CollectivePreset.Should().BeNull();
        config.CollectiveThinkingMode.Should().Be("adaptive");
        config.Failover.Should().BeTrue();
    }

    [Fact]
    public void ElectionStrategy_DefaultToWeightedMajority()
    {
        var config = new OuroborosConfig();

        config.ElectionStrategy.Should().Be("weighted-majority");
        config.MasterModel.Should().BeNull();
        config.EvaluationCriteria.Should().Be("default");
    }

    [Fact]
    public void Avatar_DefaultsToEnabled()
    {
        var config = new OuroborosConfig();

        config.Avatar.Should().BeTrue();
        config.AvatarCloud.Should().BeFalse();
        config.AvatarPort.Should().Be(9471);
    }

    [Fact]
    public void PipeMode_DefaultsToDisabled()
    {
        var config = new OuroborosConfig();

        config.PipeMode.Should().BeFalse();
        config.BatchFile.Should().BeNull();
        config.JsonOutput.Should().BeFalse();
        config.NoGreeting.Should().BeFalse();
    }

    [Fact]
    public void OpenClaw_DefaultValues()
    {
        var config = new OuroborosConfig();

        config.OpenClawGateway.Should().Be("ws://127.0.0.1:18789");
        config.OpenClawToken.Should().BeNull();
        config.EnableOpenClaw.Should().BeTrue();
        config.EnablePcNode.Should().BeFalse();
    }
}
