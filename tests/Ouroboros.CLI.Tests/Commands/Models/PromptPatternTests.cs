using Ouroboros.CLI.Commands;

namespace Ouroboros.Tests.CLI.Commands.Models;

[Trait("Category", "Unit")]
public class PromptPatternTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var pattern = new PromptPattern();

        pattern.Id.Should().HaveLength(8);
        pattern.Name.Should().BeEmpty();
        pattern.Template.Should().BeEmpty();
        pattern.UsageCount.Should().Be(0);
        pattern.SuccessCount.Should().Be(0);
        pattern.FailureCount.Should().Be(0);
        pattern.SuccessfulVariants.Should().BeEmpty();
        pattern.FailedVariants.Should().BeEmpty();
    }

    [Fact]
    public void SuccessRate_WithZeroUsage_Returns05()
    {
        var pattern = new PromptPattern();

        pattern.SuccessRate.Should().Be(0.5);
    }

    [Fact]
    public void SuccessRate_WithUsage_ReturnsCorrectRatio()
    {
        var pattern = new PromptPattern
        {
            UsageCount = 10,
            SuccessCount = 7,
            FailureCount = 3
        };

        pattern.SuccessRate.Should().Be(0.7);
    }

    [Fact]
    public void SuccessRate_AllSuccessful_ReturnsOne()
    {
        var pattern = new PromptPattern
        {
            UsageCount = 5,
            SuccessCount = 5,
            FailureCount = 0
        };

        pattern.SuccessRate.Should().Be(1.0);
    }

    [Fact]
    public void SuccessRate_AllFailed_ReturnsZero()
    {
        var pattern = new PromptPattern
        {
            UsageCount = 5,
            SuccessCount = 0,
            FailureCount = 5
        };

        pattern.SuccessRate.Should().Be(0.0);
    }

    [Fact]
    public void Id_IsUnique_AcrossInstances()
    {
        var pattern1 = new PromptPattern();
        var pattern2 = new PromptPattern();

        pattern1.Id.Should().NotBe(pattern2.Id);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var pattern = new PromptPattern
        {
            Name = "TestPattern",
            Template = "Use [TOOL:name args]"
        };

        pattern.Name.Should().Be("TestPattern");
        pattern.Template.Should().Be("Use [TOOL:name args]");
    }

    [Fact]
    public void SuccessfulVariants_CanBeAdded()
    {
        var pattern = new PromptPattern();
        pattern.SuccessfulVariants.Add("search_my_code");

        pattern.SuccessfulVariants.Should().ContainSingle("search_my_code");
    }
}
