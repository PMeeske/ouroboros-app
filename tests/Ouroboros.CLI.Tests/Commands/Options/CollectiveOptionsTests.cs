using System.CommandLine;
using Ouroboros.CLI.Commands.Options;

namespace Ouroboros.Tests.CLI.Commands.Options;

[Trait("Category", "Unit")]
public class CollectiveOptionsTests
{
    [Fact]
    public void ImplementsIComposableOptions()
    {
        var options = new CollectiveOptions();
        options.Should().BeAssignableTo<IComposableOptions>();
    }

    [Fact]
    public void CollectiveOption_HasDescription()
    {
        var options = new CollectiveOptions();
        options.CollectiveOption.Description.Should().Contain("CollectiveMind");
    }

    [Fact]
    public void ElectionStrategyOption_HasDescription()
    {
        var options = new CollectiveOptions();
        options.ElectionStrategyOption.Description.Should().Contain("Election strategy");
    }

    [Fact]
    public void AddToCommand_AddsAllOptions()
    {
        var options = new CollectiveOptions();
        var command = new Command("test");

        options.AddToCommand(command);

        command.Options.Should().Contain(options.CollectiveOption);
        command.Options.Should().Contain(options.MasterModelOption);
        command.Options.Should().Contain(options.ElectionStrategyOption);
        command.Options.Should().Contain(options.ShowSubgoalsOption);
        command.Options.Should().Contain(options.ParallelSubgoalsOption);
        command.Options.Should().Contain(options.DecomposeOption);
    }
}
