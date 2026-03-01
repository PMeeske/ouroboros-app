using FluentAssertions;
using Ouroboros.Application.Personality.Consciousness;
using Xunit;

namespace Ouroboros.Tests.Personality.Consciousness;

[Trait("Category", "Unit")]
public class RescorlaWagnerTests
{
    [Fact]
    public void ComputeDelta_ZeroSalience_ShouldReturnZero()
    {
        var delta = RescorlaWagner.ComputeDelta(0.0, 0.5, 1.0, 0.5);

        delta.Should().Be(0.0);
    }

    [Fact]
    public void ComputeDelta_MaxConditioningReached_ShouldReturnZero()
    {
        var delta = RescorlaWagner.ComputeDelta(0.5, 0.5, 1.0, 1.0);

        delta.Should().Be(0.0);
    }

    [Fact]
    public void ComputeDelta_Standard_ShouldReturnPositive()
    {
        // alpha=0.5, beta=0.5, lambda=1.0, sumV=0.0
        var delta = RescorlaWagner.ComputeDelta(0.5, 0.5, 1.0, 0.0);

        delta.Should().Be(0.25); // 0.5 * 0.5 * (1.0 - 0.0) = 0.25
    }

    [Fact]
    public void ComputeDelta_PartialLearning_ShouldReturnSmaller()
    {
        var delta = RescorlaWagner.ComputeDelta(0.5, 0.5, 1.0, 0.5);

        delta.Should().Be(0.125); // 0.5 * 0.5 * (1.0 - 0.5) = 0.125
    }

    [Fact]
    public void Reinforce_ShouldCallComputeDeltaWithLambda()
    {
        var result = RescorlaWagner.Reinforce(0.5, 0.5, 0.0, 1.0);

        result.Should().Be(0.25);
    }

    [Fact]
    public void Reinforce_DefaultLambda_ShouldBe1()
    {
        var result = RescorlaWagner.Reinforce(0.5, 0.5, 0.0);

        result.Should().Be(0.25);
    }

    [Fact]
    public void Extinguish_ShouldDriveTowardZero()
    {
        var result = RescorlaWagner.Extinguish(0.5, 0.5, 0.5);

        // 0.5 * 0.5 * (0.0 - 0.5) = -0.125
        result.Should().Be(-0.125);
    }

    [Fact]
    public void Extinguish_ZeroAssociation_ShouldReturnZero()
    {
        var result = RescorlaWagner.Extinguish(0.5, 0.5, 0.0);

        result.Should().Be(0.0);
    }
}
