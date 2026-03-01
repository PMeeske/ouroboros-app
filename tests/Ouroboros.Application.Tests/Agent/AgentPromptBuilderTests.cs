using FluentAssertions;
using Ouroboros.Application.Agent;
using Xunit;

namespace Ouroboros.Tests.Agent;

[Trait("Category", "Unit")]
public class AgentPromptBuilderTests
{
    [Fact]
    public void BuildToolDescriptions_ShouldContainAvailableTools()
    {
        var result = AgentPromptBuilder.BuildToolDescriptions();

        result.Should().Contain("Available Tools");
        result.Should().Contain("read_file");
        result.Should().Contain("write_file");
        result.Should().Contain("run_command");
        result.Should().Contain("Completing the Task");
    }

    [Fact]
    public void Build_WithTask_ShouldContainTask()
    {
        var result = AgentPromptBuilder.Build(
            "Fix the bug",
            "## tools",
            new List<AgentMessage>(),
            new List<string>());

        result.Should().Contain("Fix the bug");
        result.Should().Contain("Current Task");
    }

    [Fact]
    public void Build_WithHistory_ShouldContainHistory()
    {
        var history = new List<AgentMessage>
        {
            new("user", "Hello"),
            new("assistant", "Hi there")
        };

        var result = AgentPromptBuilder.Build("task", "tools", history, new List<string>());

        result.Should().Contain("Conversation History");
        result.Should().Contain("[user]");
        result.Should().Contain("[assistant]");
    }

    [Fact]
    public void Build_WithExecutedActions_ShouldContainActions()
    {
        var actions = new List<string> { "Read file.cs", "Edited line 10" };

        var result = AgentPromptBuilder.Build("task", "tools", new List<AgentMessage>(), actions);

        result.Should().Contain("Actions Taken So Far");
        result.Should().Contain("Read file.cs");
        result.Should().Contain("Edited line 10");
    }

    [Fact]
    public void Build_EmptyHistoryAndActions_ShouldNotContainSections()
    {
        var result = AgentPromptBuilder.Build(
            "task", "tools", new List<AgentMessage>(), new List<string>());

        result.Should().NotContain("Conversation History");
        result.Should().NotContain("Actions Taken So Far");
    }

    [Fact]
    public void Build_ShouldEndWithActionPrompt()
    {
        var result = AgentPromptBuilder.Build(
            "task", "tools", new List<AgentMessage>(), new List<string>());

        result.Should().Contain("Your Next Action");
    }
}
