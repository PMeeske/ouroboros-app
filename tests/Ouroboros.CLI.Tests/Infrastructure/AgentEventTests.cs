using Ouroboros.CLI.Infrastructure;

namespace Ouroboros.Tests.CLI.Infrastructure;

[Trait("Category", "Unit")]
public class AgentEventTests
{
    [Fact]
    public void ToolStartedEvent_SetsAllProperties()
    {
        var timestamp = DateTime.UtcNow;
        var evt = new ToolStartedEvent("search_my_code", "auth", timestamp);

        evt.ToolName.Should().Be("search_my_code");
        evt.Param.Should().Be("auth");
        evt.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void ToolStartedEvent_WithNullParam_AllowsNull()
    {
        var evt = new ToolStartedEvent("tool", null, DateTime.UtcNow);

        evt.Param.Should().BeNull();
    }

    [Fact]
    public void ToolCompletedEvent_SuccessCase()
    {
        var elapsed = TimeSpan.FromMilliseconds(250);
        var evt = new ToolCompletedEvent("read_file", true, "file content", elapsed);

        evt.ToolName.Should().Be("read_file");
        evt.Success.Should().BeTrue();
        evt.Output.Should().Be("file content");
        evt.Elapsed.Should().Be(elapsed);
    }

    [Fact]
    public void ToolCompletedEvent_FailureCase()
    {
        var evt = new ToolCompletedEvent("write_file", false, null, TimeSpan.Zero);

        evt.Success.Should().BeFalse();
        evt.Output.Should().BeNull();
    }

    [Fact]
    public void AgentThinkingEvent_SetsLabel()
    {
        var evt = new AgentThinkingEvent("Processing query...");

        evt.Label.Should().Be("Processing query...");
    }

    [Fact]
    public void AgentResponseEvent_SetsAllProperties()
    {
        var elapsed = TimeSpan.FromSeconds(2);
        var evt = new AgentResponseEvent("Iaret", "Hello world", elapsed);

        evt.PersonaName.Should().Be("Iaret");
        evt.Text.Should().Be("Hello world");
        evt.Elapsed.Should().Be(elapsed);
    }

    [Fact]
    public void AllEvents_InheritFromAgentEvent()
    {
        AgentEvent evt1 = new ToolStartedEvent("t", null, DateTime.UtcNow);
        AgentEvent evt2 = new ToolCompletedEvent("t", true, null, TimeSpan.Zero);
        AgentEvent evt3 = new AgentThinkingEvent("label");
        AgentEvent evt4 = new AgentResponseEvent("p", "text", TimeSpan.Zero);

        evt1.Should().BeAssignableTo<AgentEvent>();
        evt2.Should().BeAssignableTo<AgentEvent>();
        evt3.Should().BeAssignableTo<AgentEvent>();
        evt4.Should().BeAssignableTo<AgentEvent>();
    }

    [Fact]
    public void ToolStartedEvent_Equality_Works()
    {
        var ts = DateTime.UtcNow;
        var evt1 = new ToolStartedEvent("tool", "param", ts);
        var evt2 = new ToolStartedEvent("tool", "param", ts);

        evt1.Should().Be(evt2);
    }
}
