using Ouroboros.Genetic.Abstractions;

namespace Ouroboros.Application.Personality;

/// <summary>
/// Fitness function that evaluates thought coherence and novelty.
/// </summary>
public sealed class ThoughtFitness : IFitnessFunction<ThoughtGene>
{
    private readonly HashSet<string> _recentThoughts;
    private readonly InnerThoughtType _targetType;

    public ThoughtFitness(HashSet<string> recentThoughts, InnerThoughtType targetType)
    {
        _recentThoughts = recentThoughts;
        _targetType = targetType;
    }

    public Task<double> EvaluateAsync(IChromosome<ThoughtGene> chromosome)
    {
        var thought = ((ThoughtChromosome)chromosome).ComposeThought();
        double fitness = 0.0;

        // Coherence: proper length and structure
        if (thought.Length >= 20 && thought.Length <= 120)
            fitness += 0.3;

        // Novelty: penalize if too similar to recent thoughts
        if (!_recentThoughts.Any(r => thought.Contains(r) || r.Contains(thought)))
            fitness += 0.3;

        // Variety: reward diverse gene categories
        var categories = chromosome.Genes.Select(g => g.Category).Distinct().Count();
        fitness += Math.Min(0.2, categories * 0.05);

        // Weight balance: prefer balanced contributions
        var avgWeight = chromosome.Genes.Average(g => g.Weight);
        if (avgWeight > 0.3 && avgWeight < 0.8)
            fitness += 0.2;

        return Task.FromResult(fitness);
    }
}