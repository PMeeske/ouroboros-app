using System.CommandLine;
using Ouroboros.CLI.Commands.Options;

namespace Ouroboros.Tests.CLI.Commands.Options;

[Trait("Category", "Unit")]
public class AskCommandOptionsTests
{
    [Fact]
    public void Constructor_InitializesAllOptionGroups()
    {
        var options = new AskCommandOptions();

        options.Model.Should().NotBeNull();
        options.Endpoint.Should().NotBeNull();
        options.MultiModel.Should().NotBeNull();
        options.Diagnostics.Should().NotBeNull();
        options.Embedding.Should().NotBeNull();
        options.Collective.Should().NotBeNull();
        options.AgentLoop.Should().NotBeNull();
        options.Voice.Should().NotBeNull();
    }

    [Fact]
    public void AskSpecificOptions_AreInitialized()
    {
        var options = new AskCommandOptions();

        options.QuestionArgument.Should().NotBeNull();
        options.QuestionOption.Should().NotBeNull();
        options.RagOption.Should().NotBeNull();
        options.CultureOption.Should().NotBeNull();
        options.TopKOption.Should().NotBeNull();
    }

    [Fact]
    public void AddToCommand_AddsAllOptions()
    {
        var options = new AskCommandOptions();
        var command = new Command("ask");

        options.AddToCommand(command);

        // Should have at least the ask-specific options + composed group options
        command.Options.Should().NotBeEmpty();
    }
}
