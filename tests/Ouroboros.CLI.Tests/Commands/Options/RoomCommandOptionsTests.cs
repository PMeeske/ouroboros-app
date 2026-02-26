using System.CommandLine;
using Ouroboros.CLI.Commands.Options;

namespace Ouroboros.Tests.CLI.Commands.Options;

[Trait("Category", "Unit")]
public class RoomCommandOptionsTests
{
    [Fact]
    public void PersonaOption_HasDescription()
    {
        var options = new RoomCommandOptions();
        options.PersonaOption.Description.Should().Contain("Persona");
    }

    [Fact]
    public void ModelOption_HasDescription()
    {
        var options = new RoomCommandOptions();
        options.ModelOption.Description.Should().Contain("LLM model");
    }

    [Fact]
    public void EndpointOption_HasDescription()
    {
        var options = new RoomCommandOptions();
        options.EndpointOption.Description.Should().Contain("endpoint");
    }

    [Fact]
    public void CooldownOption_HasDescription()
    {
        var options = new RoomCommandOptions();
        options.CooldownOption.Description.Should().Contain("seconds between interjections");
    }

    [Fact]
    public void PhiThresholdOption_HasDescription()
    {
        var options = new RoomCommandOptions();
        options.PhiThresholdOption.Description.Should().Contain("Phi");
    }

    [Fact]
    public void CameraOption_HasDescription()
    {
        var options = new RoomCommandOptions();
        options.CameraOption.Description.Should().Contain("camera");
    }

    [Fact]
    public void AddToCommand_AddsAllOptions()
    {
        var options = new RoomCommandOptions();
        var command = new Command("test");

        options.AddToCommand(command);

        command.Options.Should().Contain(options.PersonaOption);
        command.Options.Should().Contain(options.ModelOption);
        command.Options.Should().Contain(options.EndpointOption);
        command.Options.Should().Contain(options.EmbedModelOption);
        command.Options.Should().Contain(options.QdrantEndpointOption);
        command.Options.Should().Contain(options.AzureSpeechKeyOption);
        command.Options.Should().Contain(options.AzureSpeechRegionOption);
        command.Options.Should().Contain(options.TtsVoiceOption);
        command.Options.Should().Contain(options.LocalTtsOption);
        command.Options.Should().Contain(options.AvatarOption);
        command.Options.Should().Contain(options.AvatarCloudOption);
        command.Options.Should().Contain(options.AvatarPortOption);
        command.Options.Should().Contain(options.QuietOption);
        command.Options.Should().Contain(options.CooldownOption);
        command.Options.Should().Contain(options.MaxInterjectionsOption);
        command.Options.Should().Contain(options.PhiThresholdOption);
        command.Options.Should().Contain(options.ProactiveOption);
        command.Options.Should().Contain(options.IdleDelayOption);
        command.Options.Should().Contain(options.CameraOption);
    }
}
