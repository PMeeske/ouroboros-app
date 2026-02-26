using FluentAssertions;
using Ouroboros.Application.Personality;
using Xunit;

namespace Ouroboros.Tests.Personality.Evolution;

[Trait("Category", "Unit")]
public class PersonalityGeneTests
{
    [Fact]
    public void Constructor_ShouldSetKeyAndValue()
    {
        var gene = new PersonalityGene("trait:warmth", 0.8);

        gene.Key.Should().Be("trait:warmth");
        gene.Value.Should().Be(0.8);
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new PersonalityGene("key", 0.5);
        var b = new PersonalityGene("key", 0.5);

        a.Should().Be(b);
    }
}
