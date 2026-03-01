using FluentAssertions;
using Ouroboros.Application.Personality;
using Xunit;

namespace Ouroboros.Tests.Personality.Evolution;

[Trait("Category", "Unit")]
public class PersonalityChromosomeTests
{
    private static PersonalityChromosome CreateChromosome()
    {
        return new PersonalityChromosome(new List<PersonalityGene>
        {
            new("trait:warmth", 0.8),
            new("trait:curiosity", 0.7),
            new("curiosity:math", 0.6),
            new("curiosity:art", 0.9),
            new("proactivity", 0.65),
            new("adaptability", 0.75)
        });
    }

    [Fact]
    public void GetTrait_ExistingTrait_ShouldReturnValue()
    {
        var chromosome = CreateChromosome();

        chromosome.GetTrait("warmth").Should().Be(0.8);
    }

    [Fact]
    public void GetTrait_MissingTrait_ShouldReturnDefault()
    {
        var chromosome = CreateChromosome();

        chromosome.GetTrait("nonexistent").Should().Be(0.5);
    }

    [Fact]
    public void GetCuriosity_ExistingTopic_ShouldReturnValue()
    {
        var chromosome = CreateChromosome();

        chromosome.GetCuriosity("math").Should().Be(0.6);
    }

    [Fact]
    public void ProactivityLevel_ShouldReturnCorrectValue()
    {
        var chromosome = CreateChromosome();

        chromosome.ProactivityLevel.Should().Be(0.65);
    }

    [Fact]
    public void Adaptability_ShouldReturnCorrectValue()
    {
        var chromosome = CreateChromosome();

        chromosome.Adaptability.Should().Be(0.75);
    }

    [Fact]
    public void GetTraitIntensities_ShouldReturnTraitsOnly()
    {
        var chromosome = CreateChromosome();

        var traits = chromosome.GetTraitIntensities();

        traits.Should().HaveCount(2);
        traits.Should().ContainKey("warmth");
        traits.Should().ContainKey("curiosity");
    }

    [Fact]
    public void GetCuriosityWeights_ShouldReturnCuriosityOnly()
    {
        var chromosome = CreateChromosome();

        var weights = chromosome.GetCuriosityWeights();

        weights.Should().HaveCount(2);
        weights.Should().ContainKey("math");
        weights.Should().ContainKey("art");
    }

    [Fact]
    public void WithFitness_ShouldReturnNewWithFitness()
    {
        var chromosome = CreateChromosome();

        var result = chromosome.WithFitness(0.9);

        result.Fitness.Should().Be(0.9);
        result.Genes.Should().HaveCount(chromosome.Genes.Count);
    }

    [Fact]
    public void WithGenes_ShouldReturnNewWithGenes()
    {
        var chromosome = CreateChromosome();
        var newGenes = new List<PersonalityGene> { new("trait:test", 1.0) };

        var result = chromosome.WithGenes(newGenes);

        result.Genes.Should().HaveCount(1);
    }
}
