using System.CommandLine;
using Ouroboros.CLI.Commands.Options;

namespace Ouroboros.Tests.CLI.Commands.Options;

[Trait("Category", "Unit")]
public class EndpointOptionsTests
{
    [Fact]
    public void ImplementsIComposableOptions()
    {
        var options = new EndpointOptions();
        options.Should().BeAssignableTo<IComposableOptions>();
    }

    [Fact]
    public void EndpointOption_HasDescription()
    {
        var options = new EndpointOptions();
        options.EndpointOption.Description.Should().Contain("Remote endpoint");
    }

    [Fact]
    public void ApiKeyOption_HasDescription()
    {
        var options = new EndpointOptions();
        options.ApiKeyOption.Description.Should().Contain("API key");
    }

    [Fact]
    public void EndpointTypeOption_HasDescription()
    {
        var options = new EndpointOptions();
        options.EndpointTypeOption.Description.Should().Contain("Provider type");
    }

    [Fact]
    public void AddToCommand_AddsAllOptions()
    {
        var options = new EndpointOptions();
        var command = new Command("test");

        options.AddToCommand(command);

        command.Options.Should().Contain(options.EndpointOption);
        command.Options.Should().Contain(options.ApiKeyOption);
        command.Options.Should().Contain(options.EndpointTypeOption);
    }
}
