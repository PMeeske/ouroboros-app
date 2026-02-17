using Ouroboros.Genetic.Abstractions;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Chromosome for action sequences.
/// </summary>
public sealed class ActionSequenceChromosome : IChromosome<ActionGene>
{
    public ActionSequenceChromosome(IReadOnlyList<ActionGene> genes, double fitness = 0.0)
    {
        Genes = genes;
        Fitness = fitness;
    }

    public IReadOnlyList<ActionGene> Genes { get; }
    public double Fitness { get; }

    public IChromosome<ActionGene> WithFitness(double fitness) =>
        new ActionSequenceChromosome(Genes.ToList(), fitness);

    public IChromosome<ActionGene> WithGenes(IReadOnlyList<ActionGene> genes) =>
        new ActionSequenceChromosome(genes, Fitness);
}