using FluentAssertions;
using Moq;
using Ouroboros.Application;
using Ouroboros.Application.Agent;
using Xunit;

namespace Ouroboros.Tests.Agent;

[Trait("Category", "Unit")]
public class AgentToolExecutorTests
{
    private static CliPipelineState CreateMockState()
    {
        // PipelineBranch, ToolAwareChatModel, ToolRegistry are sealed — use null!
        // Tests only exercise tool dispatch logic, never call into these dependencies.
        return new CliPipelineState
        {
            Branch = null!,
            Llm = null!,
            Tools = null!,
            Embed = Mock.Of<Ouroboros.Domain.IEmbeddingModel>()
        };
    }

    [Fact]
    public async Task ExecuteAsync_UnknownTool_ShouldReturnError()
    {
        var tools = new Dictionary<string, Func<string, CliPipelineState, Task<string>>>();
        var state = CreateMockState();

        var result = await AgentToolExecutor.ExecuteAsync(tools, "unknown", "{}", state);

        result.Should().Contain("Error: Unknown tool 'unknown'");
        result.Should().Contain("Available tools:");
    }

    [Fact]
    public async Task ExecuteAsync_KnownTool_ShouldReturnResult()
    {
        var tools = new Dictionary<string, Func<string, CliPipelineState, Task<string>>>
        {
            ["test_tool"] = (args, state) => Task.FromResult("tool result")
        };
        var state = CreateMockState();

        var result = await AgentToolExecutor.ExecuteAsync(tools, "test_tool", "{}", state);

        result.Should().Be("tool result");
    }

    [Fact]
    public async Task ExecuteAsync_ToolThrows_ShouldReturnError()
    {
        var tools = new Dictionary<string, Func<string, CliPipelineState, Task<string>>>
        {
            ["bad_tool"] = (args, state) => throw new InvalidOperationException("tool broke")
        };
        var state = CreateMockState();

        var result = await AgentToolExecutor.ExecuteAsync(tools, "bad_tool", "{}", state);

        result.Should().Contain("Error executing bad_tool");
        result.Should().Contain("tool broke");
    }
}
