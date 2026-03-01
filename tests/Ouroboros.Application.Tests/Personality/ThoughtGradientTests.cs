using FluentAssertions;
using Ouroboros.Application.Personality;
using Xunit;

namespace Ouroboros.Tests.Personality;

[Trait("Category", "Unit")]
public class ThoughtGradientTests
{
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        var gradient = new ThoughtGradient
        {
            FromThought = "thought A",
            ToThought = "thought B",
            GradientVector = new float[] { 0.1f, 0.2f, 0.3f },
            Similarity = 0.85f,
            TransitionMagnitude = 0.42f
        };

        gradient.FromThought.Should().Be("thought A");
        gradient.ToThought.Should().Be("thought B");
        gradient.GradientVector.Should().HaveCount(3);
        gradient.Similarity.Should().Be(0.85f);
        gradient.TransitionMagnitude.Should().Be(0.42f);
    }
}
