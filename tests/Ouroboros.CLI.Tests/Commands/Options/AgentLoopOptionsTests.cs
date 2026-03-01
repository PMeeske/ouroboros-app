using System.CommandLine;
using Ouroboros.CLI.Commands.Options;

namespace Ouroboros.Tests.CLI.Commands.Options;

[Trait("Category", "Unit")]
public class AgentLoopOptionsTests
{
    [Fact]
    public void ImplementsIComposableOptions()
    {
        var options = new AgentLoopOptions();
        options.Should().BeAssignableTo<IComposableOptions>();
    }

    [Fact]
    public void AgentOption_HasCorrectDescription()
    {
        var options = new AgentLoopOptions();
        options.AgentOption.Description.Should().Contain("agent loop");
    }

    [Fact]
    public void AgentModeOption_HasCorrectDescription()
    {
        var options = new AgentLoopOptions();
        options.AgentModeOption.Description.Should().Contain("Agent implementation");
    }

    [Fact]
    public void AgentMaxStepsOption_HasCorrectDescription()
    {
        var options = new AgentLoopOptions();
        options.AgentMaxStepsOption.Description.Should().Contain("Max iterations");
    }

    [Fact]
    public void AddToCommand_AddsAllOptions()
    {
        var options = new AgentLoopOptions();
        var command = new Command("test");

        options.AddToCommand(command);

        command.Options.Should().Contain(options.AgentOption);
        command.Options.Should().Contain(options.AgentModeOption);
        command.Options.Should().Contain(options.AgentMaxStepsOption);
    }
}
