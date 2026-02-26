using Ouroboros.CLI.Commands;

namespace Ouroboros.Tests.CLI.Commands.Models;

[Trait("Category", "Unit")]
public class PromptOptimizerTests
{
    private readonly PromptOptimizer _optimizer = new();

    [Fact]
    public void Constructor_InitializesDefaultPatterns()
    {
        var stats = _optimizer.GetStatistics();

        stats.Should().Contain("Prompt Optimization Statistics");
        stats.Should().Contain("Basic Tool Syntax");
        stats.Should().Contain("Emphatic Tool Syntax");
    }

    [Fact]
    public void RecordOutcome_SuccessfulOutcome_DoesNotThrow()
    {
        var outcome = new InteractionOutcome(
            "search auth",
            "[TOOL:search_my_code auth]",
            new List<string> { "search_my_code" },
            new List<string> { "search_my_code" },
            true,
            TimeSpan.FromMilliseconds(100));

        var act = () => _optimizer.RecordOutcome(outcome);

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordOutcome_FailedOutcome_DoesNotThrow()
    {
        var outcome = new InteractionOutcome(
            "search auth",
            "Auth is a concept...",
            new List<string> { "search_my_code" },
            new List<string>(),
            false,
            TimeSpan.FromMilliseconds(200));

        var act = () => _optimizer.RecordOutcome(outcome);

        act.Should().NotThrow();
    }

    [Fact]
    public void SelectBestPattern_ExistingCategory_ReturnsPattern()
    {
        var pattern = _optimizer.SelectBestPattern("syntax");

        pattern.Should().NotBeNull();
        pattern.Name.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SelectBestPattern_NonExistentCategory_ReturnsFirstPattern()
    {
        var pattern = _optimizer.SelectBestPattern("nonexistent_xyz_category");

        pattern.Should().NotBeNull();
    }

    [Fact]
    public void GenerateOptimizedToolInstruction_ReturnsNonEmptyString()
    {
        var tools = new List<string> { "search_my_code", "read_my_file", "calculator" };

        var instruction = _optimizer.GenerateOptimizedToolInstruction(tools, "search for auth");

        instruction.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GetStatistics_ReturnsFormattedStatistics()
    {
        var stats = _optimizer.GetStatistics();

        stats.Should().Contain("Prompt Optimization Statistics");
        stats.Should().Contain("Total interactions tracked:");
        stats.Should().Contain("Learned weights:");
        stats.Should().Contain("Pattern Performance:");
        stats.Should().Contain("Overall Success Rate:");
    }

    [Fact]
    public void DetectExpectedTools_SearchInput_ReturnsSearchTool()
    {
        var tools = _optimizer.DetectExpectedTools("search for authentication patterns");

        tools.Should().Contain("search_my_code");
    }

    [Fact]
    public void DetectExpectedTools_ReadInput_ReturnsReadTool()
    {
        var tools = _optimizer.DetectExpectedTools("read the file src/main.cs");

        tools.Should().Contain("read_my_file");
    }

    [Fact]
    public void DetectExpectedTools_ModifyInput_ReturnsModifyTool()
    {
        var tools = _optimizer.DetectExpectedTools("modify the code to fix the bug");

        tools.Should().Contain("modify_my_code");
    }

    [Fact]
    public void DetectExpectedTools_CalculateInput_ReturnsCalculator()
    {
        var tools = _optimizer.DetectExpectedTools("calculate 2 + 3");

        tools.Should().Contain("calculator");
    }

    [Fact]
    public void DetectExpectedTools_MathExpression_ReturnsCalculator()
    {
        var tools = _optimizer.DetectExpectedTools("what is 42 * 7");

        tools.Should().Contain("calculator");
    }

    [Fact]
    public void DetectExpectedTools_WebInput_ReturnsWebTool()
    {
        var tools = _optimizer.DetectExpectedTools("search online for latest news");

        tools.Should().Contain("web_research");
    }

    [Fact]
    public void DetectExpectedTools_NoToolInput_ReturnsEmpty()
    {
        var tools = _optimizer.DetectExpectedTools("hello, how are you?");

        tools.Should().BeEmpty();
    }

    [Fact]
    public void ExtractToolCalls_ValidToolCalls_ExtractsCorrectly()
    {
        var response = "Let me search: [TOOL:search_my_code auth] and also [TOOL:read_my_file config.cs]";

        var calls = _optimizer.ExtractToolCalls(response);

        calls.Should().HaveCount(2);
        calls.Should().Contain("search_my_code");
        calls.Should().Contain("read_my_file");
    }

    [Fact]
    public void ExtractToolCalls_NoToolCalls_ReturnsEmpty()
    {
        var response = "There are no tool calls in this response.";

        var calls = _optimizer.ExtractToolCalls(response);

        calls.Should().BeEmpty();
    }

    [Fact]
    public void RecordOutcome_MultipleOutcomes_TracksAll()
    {
        for (int i = 0; i < 5; i++)
        {
            _optimizer.RecordOutcome(new InteractionOutcome(
                $"input {i}", $"response {i}",
                new List<string>(), new List<string>(),
                i % 2 == 0, TimeSpan.FromMilliseconds(100)));
        }

        var stats = _optimizer.GetStatistics();
        stats.Should().Contain("Total interactions tracked: 5");
    }
}
