using System.CommandLine;
using Ouroboros.CLI.Commands.Options;

namespace Ouroboros.Tests.CLI.Commands.Options;

[Trait("Category", "Unit")]
public class EmbeddingOptionsTests
{
    [Fact]
    public void ImplementsIComposableOptions()
    {
        var options = new EmbeddingOptions();
        options.Should().BeAssignableTo<IComposableOptions>();
    }

    [Fact]
    public void EmbedModelOption_HasDescription()
    {
        var options = new EmbeddingOptions();
        options.EmbedModelOption.Description.Should().Contain("Embedding model");
    }

    [Fact]
    public void QdrantEndpointOption_HasDescription()
    {
        var options = new EmbeddingOptions();
        options.QdrantEndpointOption.Description.Should().Contain("Qdrant");
    }

    [Fact]
    public void AddToCommand_AddsAllOptions()
    {
        var options = new EmbeddingOptions();
        var command = new Command("test");

        options.AddToCommand(command);

        command.Options.Should().Contain(options.EmbedModelOption);
        command.Options.Should().Contain(options.QdrantEndpointOption);
    }
}
