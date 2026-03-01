using System.CommandLine;
using Ouroboros.CLI.Commands.Options;

namespace Ouroboros.Tests.CLI.Commands.Options;

[Trait("Category", "Unit")]
public class DiagnosticOptionsTests
{
    [Fact]
    public void ImplementsIComposableOptions()
    {
        var options = new DiagnosticOptions();
        options.Should().BeAssignableTo<IComposableOptions>();
    }

    [Fact]
    public void DebugOption_HasDescription()
    {
        var options = new DiagnosticOptions();
        options.DebugOption.Description.Should().Contain("debug logging");
    }

    [Fact]
    public void StrictModelOption_HasDescription()
    {
        var options = new DiagnosticOptions();
        options.StrictModelOption.Description.Should().Contain("Fail instead");
    }

    [Fact]
    public void JsonToolsOption_HasDescription()
    {
        var options = new DiagnosticOptions();
        options.JsonToolsOption.Description.Should().Contain("JSON tool call");
    }

    [Fact]
    public void AddToCommand_AddsAllOptions()
    {
        var options = new DiagnosticOptions();
        var command = new Command("test");

        options.AddToCommand(command);

        command.Options.Should().Contain(options.DebugOption);
        command.Options.Should().Contain(options.StrictModelOption);
        command.Options.Should().Contain(options.JsonToolsOption);
    }
}
