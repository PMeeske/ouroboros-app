using System.CommandLine;
using Ouroboros.CLI.Commands.Options;

namespace Ouroboros.Tests.CLI.Commands.Options;

[Trait("Category", "Unit")]
public class MultiModelOptionsTests
{
    [Fact]
    public void ImplementsIComposableOptions()
    {
        var options = new MultiModelOptions();
        options.Should().BeAssignableTo<IComposableOptions>();
    }

    [Fact]
    public void RouterOption_HasDescription()
    {
        var options = new MultiModelOptions();
        options.RouterOption.Description.Should().Contain("multi-model routing");
    }

    [Fact]
    public void CoderModelOption_HasDescription()
    {
        var options = new MultiModelOptions();
        options.CoderModelOption.Description.Should().Contain("code");
    }

    [Fact]
    public void AddToCommand_AddsAllOptions()
    {
        var options = new MultiModelOptions();
        var command = new Command("test");

        options.AddToCommand(command);

        command.Options.Should().Contain(options.RouterOption);
        command.Options.Should().Contain(options.CoderModelOption);
        command.Options.Should().Contain(options.SummarizeModelOption);
        command.Options.Should().Contain(options.ReasonModelOption);
        command.Options.Should().Contain(options.GeneralModelOption);
    }
}
