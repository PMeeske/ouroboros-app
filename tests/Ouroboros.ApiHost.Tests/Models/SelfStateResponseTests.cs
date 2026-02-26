using Ouroboros.ApiHost.Models;

namespace Ouroboros.Tests.Models;

[Trait("Category", "Unit")]
public sealed class SelfStateResponseTests
{
    [Fact]
    public void Constructor_WithRequiredProperties_SetsValues()
    {
        // Arrange
        var agentId = Guid.NewGuid();

        // Act
        var response = new SelfStateResponse
        {
            AgentId = agentId,
            Name = "TestAgent"
        };

        // Assert
        response.AgentId.Should().Be(agentId);
        response.Name.Should().Be("TestAgent");
    }

    [Fact]
    public void DefaultValues_OptionalProperties_AreDefaults()
    {
        // Arrange & Act
        var response = new SelfStateResponse
        {
            AgentId = Guid.Empty,
            Name = "test"
        };

        // Assert
        response.CapabilityCount.Should().Be(0);
        response.Resources.Should().BeNull();
        response.Commitments.Should().BeNull();
        response.Performance.Should().BeNull();
        response.StateTimestamp.Should().Be(default);
    }

    [Fact]
    public void Properties_SetAll_RetainValues()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var commitments = new List<CommitmentDto>
        {
            new() { Id = Guid.NewGuid(), Description = "task1", Status = "Active" }
        };
        var performance = new PerformanceDto { TotalTasks = 100 };
        var resources = new Dictionary<string, object> { ["cpu"] = 80.0 };

        // Act
        var response = new SelfStateResponse
        {
            AgentId = agentId,
            Name = "Agent-1",
            CapabilityCount = 5,
            Resources = resources,
            Commitments = commitments,
            Performance = performance,
            StateTimestamp = timestamp
        };

        // Assert
        response.CapabilityCount.Should().Be(5);
        response.Resources.Should().ContainKey("cpu");
        response.Commitments.Should().HaveCount(1);
        response.Performance!.TotalTasks.Should().Be(100);
        response.StateTimestamp.Should().Be(timestamp);
    }
}
