using Ouroboros.CLI.Infrastructure;

namespace Ouroboros.Tests.CLI.Infrastructure;

[Trait("Category", "Unit")]
public class SlashCommandResultTests
{
    [Fact]
    public void Handled_WithNoOutput_CreatesHandledResult()
    {
        var result = SlashCommandResult.Handled();

        result.WasHandled.Should().BeTrue();
        result.Output.Should().BeNull();
        result.ShouldExit.Should().BeFalse();
    }

    [Fact]
    public void Handled_WithOutput_SetsOutput()
    {
        var result = SlashCommandResult.Handled("done");

        result.WasHandled.Should().BeTrue();
        result.Output.Should().Be("done");
    }

    [Fact]
    public void Exit_CreatesShouldExitResult()
    {
        var result = SlashCommandResult.Exit();

        result.WasHandled.Should().BeTrue();
        result.ShouldExit.Should().BeTrue();
    }

    [Fact]
    public void Exit_WithOutput_SetsOutput()
    {
        var result = SlashCommandResult.Exit("goodbye");

        result.ShouldExit.Should().BeTrue();
        result.Output.Should().Be("goodbye");
    }

    [Fact]
    public void Unhandled_CreatesUnhandledResult()
    {
        var result = SlashCommandResult.Unhandled();

        result.WasHandled.Should().BeFalse();
        result.Output.Should().BeNull();
    }

    [Fact]
    public void Unhandled_WithReason_SetsOutput()
    {
        var result = SlashCommandResult.Unhandled("no handler");

        result.WasHandled.Should().BeFalse();
        result.Output.Should().Be("no handler");
    }
}
