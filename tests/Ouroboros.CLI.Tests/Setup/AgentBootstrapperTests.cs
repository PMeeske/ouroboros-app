using Microsoft.Extensions.Configuration;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Setup;
using Ouroboros.Options;

namespace Ouroboros.Tests.CLI.Setup;

[Trait("Category", "Unit")]
public class AgentBootstrapperTests
{
    [Fact]
    public void AgentBootstrapper_IsStaticClass()
    {
        typeof(AgentBootstrapper).IsAbstract.Should().BeTrue();
        typeof(AgentBootstrapper).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void LoadConfiguration_ReturnsNonNull()
    {
        var config = AgentBootstrapper.LoadConfiguration();
        config.Should().NotBeNull();
        config.Should().BeAssignableTo<IConfiguration>();
    }

    [Fact]
    public void CreateConfig_FromOuroborosOptions_ReturnsConfig()
    {
        var opts = new OuroborosOptions();

        var config = AgentBootstrapper.CreateConfig(opts);

        config.Should().NotBeNull();
        config.Should().BeOfType<OuroborosConfig>();
    }

    [Fact]
    public void CreateConfig_FromOuroborosOptions_SetsPersona()
    {
        var opts = new OuroborosOptions { Persona = "TestBot" };

        var config = AgentBootstrapper.CreateConfig(opts);

        config.Persona.Should().Be("TestBot");
    }

    [Fact]
    public void CreateConfig_FromOuroborosOptions_SetsModel()
    {
        var opts = new OuroborosOptions { Model = "llama3" };

        var config = AgentBootstrapper.CreateConfig(opts);

        config.Model.Should().Be("llama3");
    }

    [Fact]
    public void CreateConfig_FromOuroborosOptions_DisablesVoiceInPushMode()
    {
        var opts = new OuroborosOptions { Push = true, PushVoice = false };

        var config = AgentBootstrapper.CreateConfig(opts);

        config.Voice.Should().BeFalse();
    }

    [Fact]
    public void CreateConfig_FromOuroborosOptions_EnablesPushVoiceInPushMode()
    {
        var opts = new OuroborosOptions { Push = true, PushVoice = true };

        var config = AgentBootstrapper.CreateConfig(opts);

        config.Voice.Should().BeTrue();
    }

    [Fact]
    public void CreateConfig_FromOuroborosOptions_InvertsNoFlags()
    {
        var opts = new OuroborosOptions
        {
            NoSkills = true,
            NoMeTTa = true,
            NoTools = true,
            NoPersonality = true,
            NoMind = true,
            NoBrowser = true
        };

        var config = AgentBootstrapper.CreateConfig(opts);

        config.EnableSkills.Should().BeFalse();
        config.EnableMeTTa.Should().BeFalse();
        config.EnableTools.Should().BeFalse();
        config.EnablePersonality.Should().BeFalse();
        config.EnableMind.Should().BeFalse();
        config.EnableBrowser.Should().BeFalse();
    }

    [Fact]
    public void CreateConfig_FromAssistOptions_ReturnsConfig()
    {
        var opts = new AssistOptions();

        var config = AgentBootstrapper.CreateConfig(opts);

        config.Should().NotBeNull();
        config.Should().BeOfType<OuroborosConfig>();
    }

    [Fact]
    public void CreateConfig_FromAssistOptions_SetsDefaults()
    {
        var opts = new AssistOptions { Persona = "Assistant", Model = "llama3" };

        var config = AgentBootstrapper.CreateConfig(opts);

        config.Persona.Should().Be("Assistant");
        config.Model.Should().Be("llama3");
    }

    [Fact]
    public void CreateConfig_FromOuroborosOptions_SetsNoGreetingForPipeMode()
    {
        var opts = new OuroborosOptions { Pipe = true };

        var config = AgentBootstrapper.CreateConfig(opts);

        config.NoGreeting.Should().BeTrue();
    }

    [Fact]
    public void CreateConfig_FromOuroborosOptions_SetsNoGreetingForBatchFile()
    {
        var opts = new OuroborosOptions { BatchFile = "batch.txt" };

        var config = AgentBootstrapper.CreateConfig(opts);

        config.NoGreeting.Should().BeTrue();
    }

    [Fact]
    public void CreateConfig_FromOuroborosOptions_SetsNoGreetingForExec()
    {
        var opts = new OuroborosOptions { Exec = "test command" };

        var config = AgentBootstrapper.CreateConfig(opts);

        config.NoGreeting.Should().BeTrue();
    }

    [Fact]
    public void CreateConfig_FromOuroborosOptions_SetsMultiModelOptions()
    {
        var opts = new OuroborosOptions
        {
            CoderModel = "coder-model",
            ReasonModel = "reason-model",
            SummarizeModel = "summarize-model",
            VisionModel = "vision-model"
        };

        var config = AgentBootstrapper.CreateConfig(opts);

        config.CoderModel.Should().Be("coder-model");
        config.ReasonModel.Should().Be("reason-model");
        config.SummarizeModel.Should().Be("summarize-model");
        config.VisionModel.Should().Be("vision-model");
    }
}
