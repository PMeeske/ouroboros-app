using System.CommandLine;
using Ouroboros.CLI.Commands.Options;

namespace Ouroboros.Tests.CLI.Commands.Options;

[Trait("Category", "Unit")]
public class ImmersiveCommandOptionsTests
{
    [Fact]
    public void PersonaOption_HasDescription()
    {
        var options = new ImmersiveCommandOptions();
        options.PersonaOption.Description.Should().Contain("Persona");
    }

    [Fact]
    public void ModelOption_HasDescription()
    {
        var options = new ImmersiveCommandOptions();
        options.ModelOption.Description.Should().Contain("LLM model");
    }

    [Fact]
    public void VoiceModeOption_HasDescription()
    {
        var options = new ImmersiveCommandOptions();
        options.VoiceModeOption.Description.Should().Contain("voice interaction");
    }

    [Fact]
    public void AvatarOption_HasDescription()
    {
        var options = new ImmersiveCommandOptions();
        options.AvatarOption.Description.Should().Contain("avatar viewer");
    }

    [Fact]
    public void RoomModeOption_HasDescription()
    {
        var options = new ImmersiveCommandOptions();
        options.RoomModeOption.Description.Should().Contain("room listening");
    }

    [Fact]
    public void EnableOpenClawOption_HasDescription()
    {
        var options = new ImmersiveCommandOptions();
        options.EnableOpenClawOption.Description.Should().Contain("OpenClaw");
    }

    [Fact]
    public void AddToCommand_AddsAllOptions()
    {
        var options = new ImmersiveCommandOptions();
        var command = new Command("test");

        options.AddToCommand(command);

        command.Options.Should().Contain(options.PersonaOption);
        command.Options.Should().Contain(options.ModelOption);
        command.Options.Should().Contain(options.EndpointOption);
        command.Options.Should().Contain(options.EmbedModelOption);
        command.Options.Should().Contain(options.QdrantEndpointOption);
        command.Options.Should().Contain(options.VoiceModeOption);
        command.Options.Should().Contain(options.VoiceOnlyOption);
        command.Options.Should().Contain(options.LocalTtsOption);
        command.Options.Should().Contain(options.AzureTtsOption);
        command.Options.Should().Contain(options.AzureSpeechKeyOption);
        command.Options.Should().Contain(options.AzureSpeechRegionOption);
        command.Options.Should().Contain(options.TtsVoiceOption);
        command.Options.Should().Contain(options.AvatarOption);
        command.Options.Should().Contain(options.AvatarCloudOption);
        command.Options.Should().Contain(options.AvatarPortOption);
        command.Options.Should().Contain(options.RoomModeOption);
        command.Options.Should().Contain(options.EnableOpenClawOption);
        command.Options.Should().Contain(options.OpenClawGatewayOption);
        command.Options.Should().Contain(options.OpenClawTokenOption);
    }
}
