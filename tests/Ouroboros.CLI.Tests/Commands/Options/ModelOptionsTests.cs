using System.CommandLine;
using Ouroboros.CLI.Commands.Options;

namespace Ouroboros.Tests.CLI.Commands.Options;

[Trait("Category", "Unit")]
public class ModelOptionsTests
{
    [Fact]
    public void ModelOption_HasCorrectDefaults()
    {
        var options = new ModelOptions();

        options.ModelOption.Should().NotBeNull();
        options.TemperatureOption.Should().NotBeNull();
        options.MaxTokensOption.Should().NotBeNull();
        options.TimeoutSecondsOption.Should().NotBeNull();
        options.StreamOption.Should().NotBeNull();
    }

    [Fact]
    public void ImplementsIComposableOptions()
    {
        var options = new ModelOptions();
        options.Should().BeAssignableTo<IComposableOptions>();
    }

    [Fact]
    public void AddToCommand_AddsAllOptions()
    {
        var options = new ModelOptions();
        var command = new Command("test");

        options.AddToCommand(command);

        command.Options.Should().HaveCountGreaterThanOrEqualTo(5);
    }
}
