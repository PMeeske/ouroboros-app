using System.CommandLine;
using Ouroboros.CLI.Commands.Options;

namespace Ouroboros.Tests.CLI.Commands.Options;

[Trait("Category", "Unit")]
public class SkillsCommandOptionsTests
{
    [Fact]
    public void ComposedGroups_AreInitialized()
    {
        var options = new SkillsCommandOptions();

        options.Model.Should().NotBeNull();
        options.Endpoint.Should().NotBeNull();
        options.MultiModel.Should().NotBeNull();
        options.Diagnostics.Should().NotBeNull();
        options.Embedding.Should().NotBeNull();
    }

    [Fact]
    public void ListOption_HasDescription()
    {
        var options = new SkillsCommandOptions();
        options.ListOption.Description.Should().Contain("List all skills");
    }

    [Fact]
    public void FetchOption_HasDescription()
    {
        var options = new SkillsCommandOptions();
        options.FetchOption.Description.Should().Contain("Fetch research");
    }

    [Fact]
    public void AddToCommand_AddsAllOptions()
    {
        var options = new SkillsCommandOptions();
        var command = new Command("test");

        options.AddToCommand(command);

        command.Options.Should().Contain(options.ListOption);
        command.Options.Should().Contain(options.FetchOption);
        command.Options.Should().Contain(options.CultureOption);
        command.Options.Should().Contain(options.Model.ModelOption);
        command.Options.Should().Contain(options.Embedding.EmbedModelOption);
    }
}
