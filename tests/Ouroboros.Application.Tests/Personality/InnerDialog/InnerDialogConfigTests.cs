using FluentAssertions;
using Ouroboros.Application.Personality;
using Xunit;

namespace Ouroboros.Tests.Personality.InnerDialog;

[Trait("Category", "Unit")]
public class InnerDialogConfigTests
{
    [Fact]
    public void Default_ShouldHaveExpectedValues()
    {
        var config = InnerDialogConfig.Default;

        config.EnableEmotionalProcessing.Should().BeTrue();
        config.EnableMemoryRecall.Should().BeTrue();
        config.EnableEthicalChecks.Should().BeTrue();
        config.EnableCreativeThinking.Should().BeTrue();
        config.EnableAutonomousThoughts.Should().BeFalse();
        config.MaxThoughts.Should().Be(10);
    }

    [Fact]
    public void Fast_ShouldHaveReducedSettings()
    {
        var config = InnerDialogConfig.Fast;

        config.MaxThoughts.Should().BeLessThan(InnerDialogConfig.Default.MaxThoughts);
        config.EnableMemoryRecall.Should().BeFalse();
        config.EnableEthicalChecks.Should().BeFalse();
    }

    [Fact]
    public void Deep_ShouldHaveExpandedSettings()
    {
        var config = InnerDialogConfig.Deep;

        config.MaxThoughts.Should().BeGreaterThan(InnerDialogConfig.Default.MaxThoughts);
        config.EnableAutonomousThoughts.Should().BeTrue();
    }

    [Fact]
    public void Autonomous_ShouldEnableAutonomous()
    {
        var config = InnerDialogConfig.Autonomous;

        config.EnableAutonomousThoughts.Should().BeTrue();
        config.AutonomousThoughtProbability.Should().Be(1.0);
    }

    [Fact]
    public void IsThoughtTypeEnabled_EmptyList_ShouldReturnTrue()
    {
        var config = InnerDialogConfig.Default;

        config.IsThoughtTypeEnabled(InnerThoughtType.Curiosity).Should().BeTrue();
    }

    [Fact]
    public void IsThoughtTypeEnabled_SpecificList_ShouldCheckContainment()
    {
        var config = InnerDialogConfig.Fast;

        config.IsThoughtTypeEnabled(InnerThoughtType.Observation).Should().BeTrue();
        config.IsThoughtTypeEnabled(InnerThoughtType.Curiosity).Should().BeFalse();
    }

    [Fact]
    public void IsProviderEnabled_EmptyList_ShouldReturnTrue()
    {
        var config = InnerDialogConfig.Default;

        config.IsProviderEnabled("any-provider").Should().BeTrue();
    }
}
