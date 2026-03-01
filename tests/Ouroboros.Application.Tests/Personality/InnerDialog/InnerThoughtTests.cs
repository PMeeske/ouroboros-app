using FluentAssertions;
using Ouroboros.Application.Personality;
using Xunit;

namespace Ouroboros.Tests.Personality.InnerDialog;

[Trait("Category", "Unit")]
public class InnerThoughtTests
{
    [Fact]
    public void Create_ShouldSetDefaults()
    {
        var thought = InnerThought.Create(InnerThoughtType.Observation, "test");

        thought.Type.Should().Be(InnerThoughtType.Observation);
        thought.Content.Should().Be("test");
        thought.Confidence.Should().Be(0.7);
        thought.Origin.Should().Be(ThoughtOrigin.Reactive);
        thought.IsAutonomous.Should().BeFalse();
    }

    [Fact]
    public void CreateAutonomous_ShouldSetAutonomousOrigin()
    {
        var thought = InnerThought.CreateAutonomous(
            InnerThoughtType.Curiosity, "wondering about...");

        thought.Origin.Should().Be(ThoughtOrigin.Autonomous);
        thought.IsAutonomous.Should().BeTrue();
        thought.Priority.Should().Be(ThoughtPriority.Background);
    }

    [Fact]
    public void CreateChained_ShouldSetParentId()
    {
        var parentId = Guid.NewGuid();
        var thought = InnerThought.CreateChained(parentId, InnerThoughtType.Analytical, "analysis");

        thought.ParentThoughtId.Should().Be(parentId);
        thought.Origin.Should().Be(ThoughtOrigin.Chained);
    }

    [Fact]
    public void IsChainParent_NullParentReactive_ShouldBeTrue()
    {
        var thought = InnerThought.Create(InnerThoughtType.Observation, "test");

        thought.IsChainParent.Should().BeTrue();
    }

    [Fact]
    public void IsChainParent_ChainedThought_ShouldBeFalse()
    {
        var thought = InnerThought.CreateChained(Guid.NewGuid(), InnerThoughtType.Analytical, "x");

        thought.IsChainParent.Should().BeFalse();
    }

    [Fact]
    public void InnerThoughtType_ShouldHaveAllValues()
    {
        Enum.GetValues<InnerThoughtType>().Should().HaveCountGreaterThanOrEqualTo(10);
    }

    [Fact]
    public void ThoughtPriority_ShouldHaveExpectedOrder()
    {
        ((int)ThoughtPriority.Background).Should().BeLessThan((int)ThoughtPriority.Low);
        ((int)ThoughtPriority.Low).Should().BeLessThan((int)ThoughtPriority.Normal);
        ((int)ThoughtPriority.Normal).Should().BeLessThan((int)ThoughtPriority.High);
        ((int)ThoughtPriority.High).Should().BeLessThan((int)ThoughtPriority.Urgent);
    }

    [Fact]
    public void ThoughtOrigin_ShouldHave5Values()
    {
        Enum.GetValues<ThoughtOrigin>().Should().HaveCount(5);
    }
}
