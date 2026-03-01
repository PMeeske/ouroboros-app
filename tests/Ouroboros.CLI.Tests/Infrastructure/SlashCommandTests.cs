using Ouroboros.CLI.Infrastructure;

namespace Ouroboros.Tests.CLI.Infrastructure;

[Trait("Category", "Unit")]
public class SlashCommandTests
{
    [Fact]
    public void Constructor_RequiredFieldsOnly()
    {
        var cmd = new SlashCommand("test", "Description");

        cmd.Name.Should().Be("test");
        cmd.Description.Should().Be("Description");
        cmd.Shortcut.Should().BeNull();
        cmd.Execute.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithAllFields()
    {
        Func<string[], CancellationToken, Task<SlashCommandResult>> handler =
            (args, ct) => Task.FromResult(SlashCommandResult.Handled());

        var cmd = new SlashCommand("test", "Description", "ctrl+t", handler);

        cmd.Name.Should().Be("test");
        cmd.Description.Should().Be("Description");
        cmd.Shortcut.Should().Be("ctrl+t");
        cmd.Execute.Should().NotBeNull();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var cmd1 = new SlashCommand("test", "desc");
        var cmd2 = new SlashCommand("test", "desc");

        cmd1.Should().Be(cmd2);
    }

    [Fact]
    public void Equality_DifferentNames_AreNotEqual()
    {
        var cmd1 = new SlashCommand("test1", "desc");
        var cmd2 = new SlashCommand("test2", "desc");

        cmd1.Should().NotBe(cmd2);
    }
}
