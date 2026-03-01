using System.CommandLine;
using Ouroboros.CLI.Commands.Options;

namespace Ouroboros.Tests.CLI.Commands.Options;

[Trait("Category", "Unit")]
public class OrchestratorCommandOptionsTests
{
    [Fact]
    public void ComposedGroups_AreInitialized()
    {
        var options = new OrchestratorCommandOptions();

        options.Model.Should().NotBeNull();
        options.Endpoint.Should().NotBeNull();
        options.MultiModel.Should().NotBeNull();
        options.Diagnostics.Should().NotBeNull();
        options.Collective.Should().NotBeNull();
    }

    [Fact]
    public void GoalOption_HasDescription()
    {
        var options = new OrchestratorCommandOptions();
        options.GoalOption.Description.Should().Contain("Goal");
    }

    [Fact]
    public void EmbedOption_HasDescription()
    {
        var options = new OrchestratorCommandOptions();
        options.EmbedOption.Description.Should().Contain("embedding model");
    }

    [Fact]
    public void AddToCommand_AddsAllOptions()
    {
        var options = new OrchestratorCommandOptions();
        var command = new Command("test");

        options.AddToCommand(command);

        command.Options.Should().Contain(options.GoalOption);
        command.Options.Should().Contain(options.CultureOption);
        command.Options.Should().Contain(options.EmbedOption);
        command.Options.Should().Contain(options.Model.ModelOption);
        command.Options.Should().Contain(options.Collective.CollectiveOption);
    }
}
