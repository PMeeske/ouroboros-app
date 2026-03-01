using Ouroboros.ApiHost.Models;

namespace Ouroboros.Tests.Models;

[Trait("Category", "Unit")]
public sealed class SelfExplainResponseTests
{
    [Fact]
    public void Constructor_WithRequiredProperties_SetsValues()
    {
        // Arrange & Act
        var response = new SelfExplainResponse
        {
            Narrative = "Agent is working",
            DagSummary = "Agent: Test\nCapabilities: 5"
        };

        // Assert
        response.Narrative.Should().Be("Agent is working");
        response.DagSummary.Should().Contain("Agent: Test");
    }

    [Fact]
    public void KeyEvents_WhenNotSet_IsNull()
    {
        // Arrange & Act
        var response = new SelfExplainResponse
        {
            Narrative = "test",
            DagSummary = "test"
        };

        // Assert
        response.KeyEvents.Should().BeNull();
    }

    [Fact]
    public void KeyEvents_WhenSet_RetainsList()
    {
        // Arrange & Act
        var events = new List<string> { "Event1", "Event2", "Event3" };
        var response = new SelfExplainResponse
        {
            Narrative = "test",
            DagSummary = "test",
            KeyEvents = events
        };

        // Assert
        response.KeyEvents.Should().HaveCount(3);
        response.KeyEvents.Should().Contain("Event2");
    }
}
