using System.CommandLine;
using Ouroboros.CLI.Commands.Options;

namespace Ouroboros.Tests.CLI.Commands.Options;

[Trait("Category", "Unit")]
public class MeTTaCommandOptionsTests
{
    [Fact]
    public void ComposedGroups_AreInitialized()
    {
        var options = new MeTTaCommandOptions();

        options.Model.Should().NotBeNull();
        options.Endpoint.Should().NotBeNull();
        options.Diagnostics.Should().NotBeNull();
        options.Embedding.Should().NotBeNull();
        options.Voice.Should().NotBeNull();
    }

    [Fact]
    public void GoalOption_HasDescription()
    {
        var options = new MeTTaCommandOptions();
        options.GoalOption.Description.Should().Contain("MeTTa orchestrator");
    }

    [Fact]
    public void InteractiveOption_HasDescription()
    {
        var options = new MeTTaCommandOptions();
        options.InteractiveOption.Description.Should().Contain("interactive MeTTa REPL");
    }

    [Fact]
    public void AddToCommand_AddsAllOptions()
    {
        var options = new MeTTaCommandOptions();
        var command = new Command("test");

        options.AddToCommand(command);

        command.Options.Should().Contain(options.GoalOption);
        command.Options.Should().Contain(options.CultureOption);
        command.Options.Should().Contain(options.PlanOnlyOption);
        command.Options.Should().Contain(options.ShowMetricsOption);
        command.Options.Should().Contain(options.InteractiveOption);
        command.Options.Should().Contain(options.PersonaOption);
        // Composed groups should also be added
        command.Options.Should().Contain(options.Model.ModelOption);
    }
}
