using Ouroboros.Genetic.Abstractions;

namespace Ouroboros.Application.Personality;

/// <summary>
/// Chromosome representing a complete thought structure.
/// </summary>
public sealed class ThoughtChromosome : IChromosome<ThoughtGene>
{
    public ThoughtChromosome(IReadOnlyList<ThoughtGene> genes, double fitness = 0.0)
    {
        Genes = genes;
        Fitness = fitness;
    }

    public IReadOnlyList<ThoughtGene> Genes { get; }
    public double Fitness { get; }

    public IChromosome<ThoughtGene> WithFitness(double fitness) =>
        new ThoughtChromosome(Genes.ToList(), fitness);

    public IChromosome<ThoughtGene> WithGenes(IReadOnlyList<ThoughtGene> genes) =>
        new ThoughtChromosome(genes, Fitness);

    /// <summary>
    /// Composes genes into a coherent thought string.
    /// </summary>
    public string ComposeThought()
    {
        var parts = Genes
            .OrderByDescending(g => g.Weight)
            .Select(g => g.Component)
            .Where(c => !string.IsNullOrWhiteSpace(c));
        return string.Join(" ", parts).Trim();
    }
}