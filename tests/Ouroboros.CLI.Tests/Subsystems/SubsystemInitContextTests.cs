using Moq;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Subsystems;

namespace Ouroboros.Tests.CLI.Subsystems;

[Trait("Category", "Unit")]
public class SubsystemInitContextTests
{
    [Fact]
    public void Constructor_SetsRequiredProperties()
    {
        var config = new OuroborosConfig();
        var output = new Mock<IConsoleOutput>().Object;
        var voiceConfig = new VoiceModeConfig(Persona: "Iaret", VoiceOnly: false, LocalTts: false,
            VoiceLoop: false, DisableStt: true,
            Model: "test", Endpoint: "http://localhost", EmbedModel: "embed", QdrantEndpoint: "http://q");
        var voiceService = new VoiceModeService(voiceConfig);
        var voice = new VoiceSubsystem(voiceService);
        var models = new ModelSubsystem();
        var tools = new ToolSubsystem();
        var memory = new MemorySubsystem();
        var cognitive = new CognitiveSubsystem();
        var autonomy = new AutonomySubsystem();
        var embodiment = new EmbodimentSubsystem();

        var ctx = new SubsystemInitContext
        {
            Config = config,
            Output = output,
            VoiceService = voiceService,
            Voice = voice,
            Models = models,
            Tools = tools,
            Memory = memory,
            Cognitive = cognitive,
            Autonomy = autonomy,
            Embodiment = embodiment
        };

        ctx.Config.Should().BeSameAs(config);
        ctx.Output.Should().BeSameAs(output);
        ctx.VoiceService.Should().BeSameAs(voiceService);
        ctx.Voice.Should().BeSameAs(voice);
        ctx.Models.Should().BeSameAs(models);
        ctx.Tools.Should().BeSameAs(tools);
        ctx.Memory.Should().BeSameAs(memory);
        ctx.Cognitive.Should().BeSameAs(cognitive);
        ctx.Autonomy.Should().BeSameAs(autonomy);
        ctx.Embodiment.Should().BeSameAs(embodiment);
    }

    [Fact]
    public void OptionalProperties_DefaultToNull()
    {
        var config = new OuroborosConfig();
        var output = new Mock<IConsoleOutput>().Object;
        var voiceConfig = new VoiceModeConfig(Persona: "Iaret", VoiceOnly: false, LocalTts: false,
            VoiceLoop: false, DisableStt: true,
            Model: "test", Endpoint: "http://localhost", EmbedModel: "embed", QdrantEndpoint: "http://q");
        var voiceService = new VoiceModeService(voiceConfig);

        var ctx = new SubsystemInitContext
        {
            Config = config,
            Output = output,
            VoiceService = voiceService,
            Voice = new VoiceSubsystem(voiceService),
            Models = new ModelSubsystem(),
            Tools = new ToolSubsystem(),
            Memory = new MemorySubsystem(),
            Cognitive = new CognitiveSubsystem(),
            Autonomy = new AutonomySubsystem(),
            Embodiment = new EmbodimentSubsystem()
        };

        ctx.StaticConfiguration.Should().BeNull();
        ctx.Services.Should().BeNull();
        ctx.RegisterCameraCaptureAction.Should().BeNull();
        ctx.PermissionBroker.Should().BeNull();
        ctx.AgentEventBus.Should().BeNull();
    }
}
