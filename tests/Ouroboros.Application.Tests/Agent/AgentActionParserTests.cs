using FluentAssertions;
using Ouroboros.Application.Agent;
using Xunit;

namespace Ouroboros.Tests.Agent;

[Trait("Category", "Unit")]
public class AgentActionParserTests
{
    [Fact]
    public void Parse_EmptyString_ShouldReturnUnknown()
    {
        var result = AgentActionParser.Parse("");

        result.Type.Should().Be(AgentActionType.Unknown);
    }

    [Fact]
    public void Parse_PlainText_ShouldReturnThink()
    {
        var result = AgentActionParser.Parse("I need to think about this");

        result.Type.Should().Be(AgentActionType.Think);
        result.Thought.Should().Be("I need to think about this");
    }

    [Fact]
    public void Parse_CompleteJson_ShouldReturnComplete()
    {
        var result = AgentActionParser.Parse("{\"complete\": true, \"summary\": \"Done\"}");

        result.Type.Should().Be(AgentActionType.Complete);
        result.Summary.Should().Be("Done");
    }

    [Fact]
    public void Parse_CompleteWithoutSummary_ShouldUseDefault()
    {
        var result = AgentActionParser.Parse("{\"complete\": true}");

        result.Type.Should().Be(AgentActionType.Complete);
        result.Summary.Should().Be("Task completed");
    }

    [Fact]
    public void Parse_ToolUseJson_ShouldReturnUseTool()
    {
        var result = AgentActionParser.Parse("{\"tool\": \"read_file\", \"args\": {\"path\": \"test.cs\"}}");

        result.Type.Should().Be(AgentActionType.UseTool);
        result.ToolName.Should().Be("read_file");
        result.ToolArgs.Should().Contain("test.cs");
    }

    [Fact]
    public void Parse_ToolUseWithStringArgs_ShouldReturnUseTool()
    {
        var result = AgentActionParser.Parse("{\"tool\": \"think\", \"args\": \"some thought\"}");

        result.Type.Should().Be(AgentActionType.UseTool);
        result.ToolName.Should().Be("think");
        result.ToolArgs.Should().Be("some thought");
    }

    [Fact]
    public void Parse_ThoughtJson_ShouldReturnThink()
    {
        var result = AgentActionParser.Parse("{\"thought\": \"I should read the file first\"}");

        result.Type.Should().Be(AgentActionType.Think);
        result.Thought.Should().Be("I should read the file first");
    }

    [Fact]
    public void Parse_JsonEmbeddedInProse_ShouldExtractAction()
    {
        var result = AgentActionParser.Parse("Let me think... {\"tool\": \"read_file\", \"args\": \"test.cs\"} ok");

        result.Type.Should().Be(AgentActionType.UseTool);
        result.ToolName.Should().Be("read_file");
    }

    [Fact]
    public void Parse_MultipleJsonObjects_ShouldPreferLast()
    {
        var result = AgentActionParser.Parse("{\"thought\": \"hmm\"} {\"complete\": true, \"summary\": \"done\"}");

        result.Type.Should().Be(AgentActionType.Complete);
    }

    [Fact]
    public void Parse_InvalidJson_ShouldFallbackToThought()
    {
        var result = AgentActionParser.Parse("{ invalid json }");

        result.Type.Should().Be(AgentActionType.Think);
    }

    [Fact]
    public void Parse_WhitespaceOnly_ShouldReturnUnknown()
    {
        var result = AgentActionParser.Parse("   ");

        result.Type.Should().Be(AgentActionType.Unknown);
    }
}
