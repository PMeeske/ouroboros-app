using System.CommandLine;
using Ouroboros.CLI.Commands.Options;

namespace Ouroboros.Tests.CLI.Commands.Options;

[Trait("Category", "Unit")]
public class PipelineCommandOptionsTests
{
    [Fact]
    public void ComposedGroups_AreInitialized()
    {
        var options = new PipelineCommandOptions();

        options.Model.Should().NotBeNull();
        options.Endpoint.Should().NotBeNull();
        options.MultiModel.Should().NotBeNull();
        options.Diagnostics.Should().NotBeNull();
        options.AgentLoop.Should().NotBeNull();
    }

    [Fact]
    public void DslOption_HasDescription()
    {
        var options = new PipelineCommandOptions();
        options.DslOption.Description.Should().Contain("Pipeline DSL");
    }

    [Fact]
    public void TopKOption_HasDescription()
    {
        var options = new PipelineCommandOptions();
        options.TopKOption.Description.Should().Contain("Similarity retrieval");
    }

    [Fact]
    public void TraceOption_HasDescription()
    {
        var options = new PipelineCommandOptions();
        options.TraceOption.Description.Should().Contain("trace output");
    }

    [Fact]
    public void AddToCommand_AddsAllOptions()
    {
        var options = new PipelineCommandOptions();
        var command = new Command("test");

        options.AddToCommand(command);

        command.Options.Should().Contain(options.DslOption);
        command.Options.Should().Contain(options.CultureOption);
        command.Options.Should().Contain(options.EmbedOption);
        command.Options.Should().Contain(options.SourceOption);
        command.Options.Should().Contain(options.TopKOption);
        command.Options.Should().Contain(options.TraceOption);
        command.Options.Should().Contain(options.CritiqueIterationsOption);
        command.Options.Should().Contain(options.Model.ModelOption);
        command.Options.Should().Contain(options.AgentLoop.AgentOption);
    }
}
