using FluentAssertions;
using Ouroboros.Application.Personality;
using Xunit;

namespace Ouroboros.Tests.Personality.Evolution;

[Trait("Category", "Unit")]
public class PersonalityFitnessTests
{
    [Fact]
    public async Task EvaluateAsync_EmptyFeedback_ShouldReturn05()
    {
        var fitness = new PersonalityFitness(new List<InteractionFeedback>());
        var chromosome = new PersonalityChromosome(new List<PersonalityGene>
        {
            new("proactivity", 0.5)
        });

        var result = await fitness.EvaluateAsync(chromosome, CancellationToken.None);

        result.Should().Be(0.5);
    }

    [Fact]
    public async Task EvaluateAsync_HighFeedback_ShouldReturnHigh()
    {
        var feedback = new List<InteractionFeedback>
        {
            new(0.9, 0.9, 0.9, 0.9, "coding", "How?", true),
            new(0.8, 0.8, 0.8, 0.8, "math", null, false)
        };
        var fitness = new PersonalityFitness(feedback);
        var chromosome = new PersonalityChromosome(new List<PersonalityGene>
        {
            new("proactivity", 0.7)
        });

        var result = await fitness.EvaluateAsync(chromosome, CancellationToken.None);

        result.Should().BeGreaterThan(0.5);
    }
}
