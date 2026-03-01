using FluentAssertions;
using Ouroboros.Application.Personality;
using Xunit;

namespace Ouroboros.Tests.Personality;

[Trait("Category", "Unit")]
public class MemoryFragmentTests
{
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var fragment = new MemoryFragment
        {
            Timestamp = now,
            UserInput = "Hello",
            Response = "Hi there",
            Summary = "Greeting exchange",
            EmotionalContext = "friendly"
        };

        fragment.Timestamp.Should().Be(now);
        fragment.UserInput.Should().Be("Hello");
        fragment.Response.Should().Be("Hi there");
        fragment.Summary.Should().Be("Greeting exchange");
        fragment.EmotionalContext.Should().Be("friendly");
    }
}
