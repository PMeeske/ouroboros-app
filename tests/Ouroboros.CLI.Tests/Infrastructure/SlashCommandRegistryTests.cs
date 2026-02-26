using Ouroboros.CLI.Infrastructure;

namespace Ouroboros.Tests.CLI.Infrastructure;

[Trait("Category", "Unit")]
public class SlashCommandRegistryTests
{
    private readonly SlashCommandRegistry _registry = new();

    [Fact]
    public void Register_Command_IsAccessibleViaAll()
    {
        _registry.Register(new SlashCommand("exit", "Exit the application"));

        _registry.All.Should().ContainSingle();
        _registry.All[0].Name.Should().Be("exit");
    }

    [Fact]
    public void Register_FluentApi_ReturnsSelf()
    {
        var result = _registry.Register(new SlashCommand("test", "Test"));

        result.Should().BeSameAs(_registry);
    }

    [Fact]
    public void Register_WithNameAndDescription_CreatesCommand()
    {
        _registry.Register("clear", "Clear screen", "ctrl+l");

        _registry.All.Should().ContainSingle();
        _registry.All[0].Name.Should().Be("clear");
        _registry.All[0].Description.Should().Be("Clear screen");
        _registry.All[0].Shortcut.Should().Be("ctrl+l");
    }

    [Fact]
    public void Find_ExistingCommand_ReturnsCommand()
    {
        _registry.Register("exit", "Exit");

        var cmd = _registry.Find("exit");

        cmd.Should().NotBeNull();
        cmd!.Name.Should().Be("exit");
    }

    [Fact]
    public void Find_CaseInsensitive_ReturnsCommand()
    {
        _registry.Register("Exit", "Exit");

        var cmd = _registry.Find("exit");

        cmd.Should().NotBeNull();
    }

    [Fact]
    public void Find_NonExistent_ReturnsNull()
    {
        var cmd = _registry.Find("nonexistent");

        cmd.Should().BeNull();
    }

    [Fact]
    public void Filter_EmptyQuery_ReturnsAll()
    {
        _registry.Register("exit", "Exit app");
        _registry.Register("clear", "Clear screen");

        var results = _registry.Filter("");

        results.Should().HaveCount(2);
    }

    [Fact]
    public void Filter_MatchesName()
    {
        _registry.Register("exit", "Exit app");
        _registry.Register("clear", "Clear screen");

        var results = _registry.Filter("exit");

        results.Should().ContainSingle();
        results[0].Name.Should().Be("exit");
    }

    [Fact]
    public void Filter_MatchesDescription()
    {
        _registry.Register("exit", "Exit the application");
        _registry.Register("clear", "Clear the screen");

        var results = _registry.Filter("screen");

        results.Should().ContainSingle();
        results[0].Name.Should().Be("clear");
    }

    [Fact]
    public void Filter_CaseInsensitive()
    {
        _registry.Register("exit", "EXIT APP");

        var results = _registry.Filter("exit");

        results.Should().ContainSingle();
    }

    [Fact]
    public async Task DispatchAsync_NonSlashInput_ReturnsNull()
    {
        var result = await _registry.DispatchAsync("not a command");

        result.Should().BeNull();
    }

    [Fact]
    public async Task DispatchAsync_EmptySlash_ReturnsNull()
    {
        var result = await _registry.DispatchAsync("/");

        result.Should().BeNull();
    }

    [Fact]
    public async Task DispatchAsync_UnknownCommand_ReturnsNull()
    {
        var result = await _registry.DispatchAsync("/unknown");

        result.Should().BeNull();
    }

    [Fact]
    public async Task DispatchAsync_CommandWithNoHandler_ReturnsUnhandled()
    {
        _registry.Register("exit", "Exit");

        var result = await _registry.DispatchAsync("/exit");

        result.Should().NotBeNull();
        result!.WasHandled.Should().BeFalse();
    }

    [Fact]
    public async Task DispatchAsync_CommandWithHandler_ExecutesHandler()
    {
        bool handlerCalled = false;
        _registry.Register(new SlashCommand("test", "Test",
            Execute: (args, ct) =>
            {
                handlerCalled = true;
                return Task.FromResult(SlashCommandResult.Handled("done"));
            }));

        var result = await _registry.DispatchAsync("/test");

        handlerCalled.Should().BeTrue();
        result.Should().NotBeNull();
        result!.WasHandled.Should().BeTrue();
        result.Output.Should().Be("done");
    }

    [Fact]
    public async Task DispatchAsync_PassesArgumentsToHandler()
    {
        string[]? receivedArgs = null;
        _registry.Register(new SlashCommand("model", "Change model",
            Execute: (args, ct) =>
            {
                receivedArgs = args;
                return Task.FromResult(SlashCommandResult.Handled());
            }));

        await _registry.DispatchAsync("/model gpt-4o fast");

        receivedArgs.Should().NotBeNull();
        receivedArgs.Should().HaveCount(2);
        receivedArgs![0].Should().Be("gpt-4o");
        receivedArgs[1].Should().Be("fast");
    }

    [Fact]
    public void Register_MultipleCommands_PreservesOrder()
    {
        _registry.Register("a", "first");
        _registry.Register("b", "second");
        _registry.Register("c", "third");

        _registry.All.Should().HaveCount(3);
        _registry.All[0].Name.Should().Be("a");
        _registry.All[1].Name.Should().Be("b");
        _registry.All[2].Name.Should().Be("c");
    }
}
