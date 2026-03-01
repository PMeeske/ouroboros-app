using FluentAssertions;
using Ouroboros.Application.Personality;
using Xunit;

namespace Ouroboros.Tests.Personality.Models;

[Trait("Category", "Unit")]
public class PersonalityTraitTests
{
    [Fact]
    public void Default_ShouldReturnTraitWithDefaultValues()
    {
        var trait = PersonalityTrait.Default("warmth");

        trait.Name.Should().Be("warmth");
        trait.Intensity.Should().Be(0.5);
        trait.ExpressionPatterns.Should().BeEmpty();
        trait.TriggerTopics.Should().BeEmpty();
        trait.EvolutionRate.Should().Be(0.1);
    }

    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        var trait = new PersonalityTrait("kindness", 0.8,
            new[] { "gentle words" },
            new[] { "helping" },
            0.05);

        trait.Name.Should().Be("kindness");
        trait.Intensity.Should().Be(0.8);
        trait.ExpressionPatterns.Should().HaveCount(1);
        trait.TriggerTopics.Should().HaveCount(1);
        trait.EvolutionRate.Should().Be(0.05);
    }
}
