using System.CommandLine;
using Ouroboros.CLI.Commands.Options;

namespace Ouroboros.Tests.CLI.Commands.Options;

[Trait("Category", "Unit")]
public class CommandVoiceOptionsTests
{
    [Fact]
    public void ImplementsIComposableOptions()
    {
        var options = new CommandVoiceOptions();
        options.Should().BeAssignableTo<IComposableOptions>();
    }

    [Fact]
    public void VoiceOnlyOption_HasDescription()
    {
        var options = new CommandVoiceOptions();
        options.VoiceOnlyOption.Description.Should().Contain("Voice-only");
    }

    [Fact]
    public void LocalTtsOption_HasDescription()
    {
        var options = new CommandVoiceOptions();
        options.LocalTtsOption.Description.Should().Contain("local TTS");
    }

    [Fact]
    public void VoiceLoopOption_HasDescription()
    {
        var options = new CommandVoiceOptions();
        options.VoiceLoopOption.Description.Should().Contain("voice conversation");
    }

    [Fact]
    public void AddToCommand_AddsAllOptions()
    {
        var options = new CommandVoiceOptions();
        var command = new Command("test");

        options.AddToCommand(command);

        command.Options.Should().Contain(options.VoiceOnlyOption);
        command.Options.Should().Contain(options.LocalTtsOption);
        command.Options.Should().Contain(options.VoiceLoopOption);
    }
}
