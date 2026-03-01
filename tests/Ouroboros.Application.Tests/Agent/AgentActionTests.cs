using FluentAssertions;
using Ouroboros.Application.Agent;
using Xunit;

namespace Ouroboros.Tests.Agent;

[Trait("Category", "Unit")]
public class AgentActionTests
{
    [Fact]
    public void AgentAction_DefaultValues_ShouldBeNull()
    {
        var action = new AgentAction();

        action.Type.Should().Be(AgentActionType.Unknown);
        action.ToolName.Should().BeNull();
        action.ToolArgs.Should().BeNull();
        action.Thought.Should().BeNull();
        action.Summary.Should().BeNull();
    }

    [Fact]
    public void AgentAction_SetProperties_ShouldRetainValues()
    {
        var action = new AgentAction
        {
            Type = AgentActionType.UseTool,
            ToolName = "read_file",
            ToolArgs = "{\"path\": \"test.cs\"}",
            Thought = "I need to read",
            Summary = "Read a file"
        };

        action.Type.Should().Be(AgentActionType.UseTool);
        action.ToolName.Should().Be("read_file");
        action.ToolArgs.Should().Be("{\"path\": \"test.cs\"}");
        action.Thought.Should().Be("I need to read");
        action.Summary.Should().Be("Read a file");
    }
}
