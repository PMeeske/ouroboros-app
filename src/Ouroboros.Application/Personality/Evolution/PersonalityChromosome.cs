using Ouroboros.Genetic.Abstractions;

namespace Ouroboros.Application.Personality;

/// <summary>
/// Chromosome for evolving personality configurations using gene-based structure.
/// </summary>
public sealed class PersonalityChromosome : IChromosome<PersonalityGene>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PersonalityChromosome"/> class.
    /// </summary>
    public PersonalityChromosome(IReadOnlyList<PersonalityGene> genes, double fitness = 0.0)
    {
        Genes = genes;
        Fitness = fitness;
    }

    /// <inheritdoc/>
    public IReadOnlyList<PersonalityGene> Genes { get; }

    /// <inheritdoc/>
    public double Fitness { get; }

    /// <inheritdoc/>
    public IChromosome<PersonalityGene> WithFitness(double fitness) =>
        new PersonalityChromosome(Genes.ToList(), fitness);

    /// <inheritdoc/>
    public IChromosome<PersonalityGene> WithGenes(IReadOnlyList<PersonalityGene> genes) =>
        new PersonalityChromosome(genes, Fitness);

    /// <summary>Gets trait intensity by name.</summary>
    public double GetTrait(string name) =>
        Genes.FirstOrDefault(g => g.Key == $"trait:{name}")?.Value ?? 0.5;

    /// <summary>Gets curiosity weight by topic.</summary>
    public double GetCuriosity(string topic) =>
        Genes.FirstOrDefault(g => g.Key == $"curiosity:{topic}")?.Value ?? 0.5;

    /// <summary>Gets the proactivity level.</summary>
    public double ProactivityLevel =>
        Genes.FirstOrDefault(g => g.Key == "proactivity")?.Value ?? 0.5;

    /// <summary>Gets the adaptability score.</summary>
    public double Adaptability =>
        Genes.FirstOrDefault(g => g.Key == "adaptability")?.Value ?? 0.5;

    /// <summary>Gets all trait intensities.</summary>
    public Dictionary<string, double> GetTraitIntensities() =>
        Genes.Where(g => g.Key.StartsWith("trait:"))
            .ToDictionary(g => g.Key.Replace("trait:", ""), g => g.Value);

    /// <summary>Gets all curiosity weights.</summary>
    public Dictionary<string, double> GetCuriosityWeights() =>
        Genes.Where(g => g.Key.StartsWith("curiosity:"))
            .ToDictionary(g => g.Key.Replace("curiosity:", ""), g => g.Value);
}