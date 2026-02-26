using FluentAssertions;
using Ouroboros.Application.Agent;
using Xunit;

namespace Ouroboros.Tests.Agent;

[Trait("Category", "Unit")]
public class AgentMessageTests
{
    [Fact]
    public void Constructor_ShouldSetRoleAndContent()
    {
        var msg = new AgentMessage("user", "Hello world");

        msg.Role.Should().Be("user");
        msg.Content.Should().Be("Hello world");
    }

    [Fact]
    public void Constructor_WithAssistantRole_ShouldWork()
    {
        var msg = new AgentMessage("assistant", "I can help");

        msg.Role.Should().Be("assistant");
        msg.Content.Should().Be("I can help");
    }

    [Fact]
    public void Constructor_WithToolRole_ShouldWork()
    {
        var msg = new AgentMessage("tool", "file contents here");

        msg.Role.Should().Be("tool");
        msg.Content.Should().Be("file contents here");
    }
}
