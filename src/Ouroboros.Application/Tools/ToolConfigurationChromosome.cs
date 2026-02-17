using Ouroboros.Genetic.Abstractions;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Chromosome representing a tool configuration.
/// </summary>
public sealed class ToolConfigurationChromosome : IChromosome<ToolConfigurationGene>
{
    public ToolConfigurationChromosome(IReadOnlyList<ToolConfigurationGene> genes, double fitness = 0.0)
    {
        Genes = genes;
        Fitness = fitness;
    }

    public IReadOnlyList<ToolConfigurationGene> Genes { get; }
    public double Fitness { get; }

    public IChromosome<ToolConfigurationGene> WithFitness(double fitness) =>
        new ToolConfigurationChromosome(Genes.ToList(), fitness);

    public IChromosome<ToolConfigurationGene> WithGenes(IReadOnlyList<ToolConfigurationGene> genes) =>
        new ToolConfigurationChromosome(genes, Fitness);

    public ToolConfiguration ToConfiguration()
    {
        var dict = Genes.ToDictionary(g => g.Key, g => g.Value);
        return new ToolConfiguration(
            Name: dict.GetValueOrDefault("name") ?? "tool",
            Description: dict.GetValueOrDefault("description") ?? string.Empty,
            SearchProvider: dict.GetValueOrDefault("searchProvider"),
            TimeoutSeconds: double.TryParse(dict.GetValueOrDefault("timeout"), out var t) ? t : 30.0,
            MaxRetries: int.TryParse(dict.GetValueOrDefault("retries"), out var r) ? r : 3,
            CacheResults: dict.GetValueOrDefault("cacheResults") == "true",
            CustomParameters: dict.GetValueOrDefault("customParams"));
    }
}