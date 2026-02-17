using System.ComponentModel.DataAnnotations;

namespace Ouroboros.Application.Integration;

/// <summary>Adapter learning configuration.</summary>
public sealed class AdapterLearningConfig
{
    /// <summary>Gets or sets rank dimension.</summary>
    [Range(1, 64)]
    public int RankDimension { get; set; } = 8;

    /// <summary>Gets or sets learning rate.</summary>
    [Range(0.0001, 0.1)]
    public double LearningRate { get; set; } = 0.001;

    /// <summary>Gets or sets maximum adapters.</summary>
    [Range(1, 100)]
    public int MaxAdapters { get; set; } = 10;

    /// <summary>Gets or sets whether pruning is enabled.</summary>
    public bool EnablePruning { get; set; } = true;
}