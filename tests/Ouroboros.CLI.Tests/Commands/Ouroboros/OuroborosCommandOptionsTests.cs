using Ouroboros.CLI.Commands;

namespace Ouroboros.Tests.CLI.Commands.Ouroboros;

[Trait("Category", "Unit")]
public class OuroborosCommandOptionsTests
{
    [Fact]
    public void OuroborosConfig_DefaultPersona_IsIaret()
    {
        var config = new OuroborosConfig();
        config.Persona.Should().Be("Iaret");
    }

    [Fact]
    public void OuroborosConfig_DefaultModel_IsDeepseek()
    {
        var config = new OuroborosConfig();
        config.Model.Should().Be("deepseek-v3.1:671b-cloud");
    }

    [Fact]
    public void OuroborosConfig_WakeWord_DefaultsToHeyIaret()
    {
        var config = new OuroborosConfig();
        config.WakeWord.Should().Be("Hey Iaret");
    }

    [Fact]
    public void OuroborosConfig_SttBackend_DefaultsToAuto()
    {
        var config = new OuroborosConfig();
        config.SttBackend.Should().Be("auto");
    }

    [Fact]
    public void OuroborosConfig_AgentMaxSteps_DefaultsTo10()
    {
        var config = new OuroborosConfig();
        config.AgentMaxSteps.Should().Be(10);
    }

    [Fact]
    public void OuroborosConfig_ThinkingInterval_DefaultsTo30()
    {
        var config = new OuroborosConfig();
        config.ThinkingIntervalSeconds.Should().Be(30);
    }

    [Fact]
    public void OuroborosConfig_IntentionInterval_DefaultsTo45()
    {
        var config = new OuroborosConfig();
        config.IntentionIntervalSeconds.Should().Be(45);
    }

    [Fact]
    public void OuroborosConfig_DiscoveryInterval_DefaultsTo90()
    {
        var config = new OuroborosConfig();
        config.DiscoveryIntervalSeconds.Should().Be(90);
    }

    [Fact]
    public void OuroborosConfig_TtsVoice_DefaultValue()
    {
        var config = new OuroborosConfig();
        config.TtsVoice.Should().Be("en-US-AvaMultilingualNeural");
    }

    [Fact]
    public void OuroborosConfig_AzureSpeechRegion_DefaultsToEastus()
    {
        var config = new OuroborosConfig();
        config.AzureSpeechRegion.Should().Be("eastus");
    }

    [Fact]
    public void OuroborosConfig_RiskLevel_DefaultsToMedium()
    {
        var config = new OuroborosConfig();
        config.RiskLevel.Should().Be("Medium");
    }

    [Fact]
    public void OuroborosConfig_AutoApproveLow_DefaultsToTrue()
    {
        var config = new OuroborosConfig();
        config.AutoApproveLow.Should().BeTrue();
    }
}
