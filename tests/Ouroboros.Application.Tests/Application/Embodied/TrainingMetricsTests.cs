using FluentAssertions;
using Ouroboros.Application.Embodied;
using Xunit;

namespace Ouroboros.Tests.Application.Embodied;

[Trait("Category", "Unit")]
public class TrainingMetricsTests
{
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        var metrics = new TrainingMetrics(0.5, 0.3, 0.8, 10.5, 64);

        metrics.PolicyLoss.Should().Be(0.5);
        metrics.ValueLoss.Should().Be(0.3);
        metrics.Entropy.Should().Be(0.8);
        metrics.AverageReward.Should().Be(10.5);
        metrics.BatchSize.Should().Be(64);
    }
}
