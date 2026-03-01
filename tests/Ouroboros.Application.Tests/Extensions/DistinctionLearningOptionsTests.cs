using FluentAssertions;
using Ouroboros.Application.Extensions;
using Xunit;

namespace Ouroboros.Tests.Extensions;

[Trait("Category", "Unit")]
public class DistinctionLearningOptionsTests
{
    [Fact]
    public void Default_ShouldHaveExpectedValues()
    {
        var options = DistinctionLearningOptions.Default;

        options.StorageConfig.Should().BeNull();
        options.EnablePipelineIntegration.Should().BeTrue();
        options.EnableBackgroundConsolidation.Should().BeTrue();
        options.ConsolidationInterval.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void Constructor_ShouldHaveDefaultValues()
    {
        var options = new DistinctionLearningOptions();

        options.EnablePipelineIntegration.Should().BeTrue();
        options.EnableBackgroundConsolidation.Should().BeTrue();
    }

    [Fact]
    public void WithInit_ShouldOverrideDefaults()
    {
        var options = new DistinctionLearningOptions
        {
            EnablePipelineIntegration = false,
            EnableBackgroundConsolidation = false,
            ConsolidationInterval = TimeSpan.FromMinutes(30)
        };

        options.EnablePipelineIntegration.Should().BeFalse();
        options.EnableBackgroundConsolidation.Should().BeFalse();
        options.ConsolidationInterval.Should().Be(TimeSpan.FromMinutes(30));
    }
}
