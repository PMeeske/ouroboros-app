using Ouroboros.ApiHost.Models;

namespace Ouroboros.Tests.Models;

[Trait("Category", "Unit")]
public sealed class SelfExplainRequestTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var request = new SelfExplainRequest();

        // Assert
        request.EventId.Should().BeNull();
        request.IncludeContext.Should().BeTrue();
        request.MaxDepth.Should().Be(5);
    }

    [Fact]
    public void Properties_SetExplicitly_RetainValues()
    {
        // Arrange & Act
        var request = new SelfExplainRequest
        {
            EventId = "event-123",
            IncludeContext = false,
            MaxDepth = 10
        };

        // Assert
        request.EventId.Should().Be("event-123");
        request.IncludeContext.Should().BeFalse();
        request.MaxDepth.Should().Be(10);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var r1 = new SelfExplainRequest { EventId = "abc", MaxDepth = 3 };
        var r2 = new SelfExplainRequest { EventId = "abc", MaxDepth = 3 };

        // Assert
        r1.Should().Be(r2);
    }
}
