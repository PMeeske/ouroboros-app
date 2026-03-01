using FluentAssertions;
using Ouroboros.Application.Configuration;
using Xunit;

namespace Ouroboros.Tests.Configuration;

[Trait("Category", "Unit")]
public class MultiModelPresetConfigTests
{
    [Fact]
    public void Defaults_ShouldHaveExpectedValues()
    {
        var config = new MultiModelPresetConfig();

        config.Name.Should().BeEmpty();
        config.Description.Should().BeEmpty();
        config.MasterRole.Should().Be("general");
        config.DefaultTemperature.Should().Be(0.7);
        config.DefaultMaxTokens.Should().Be(2048);
        config.TimeoutSeconds.Should().Be(120);
        config.EnableMetrics.Should().BeTrue();
        config.Models.Should().BeEmpty();
    }
}
