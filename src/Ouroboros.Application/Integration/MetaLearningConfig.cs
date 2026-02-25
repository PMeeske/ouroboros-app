using System.ComponentModel.DataAnnotations;

namespace Ouroboros.Application.Integration;

/// <summary>Meta-learning configuration.</summary>
public sealed class MetaLearningConfig
{
    /// <summary>Gets or sets algorithm name.</summary>
    [Required]
    public string Algorithm { get; set; } = "MAML";

    /// <summary>Gets or sets meta-learning steps.</summary>
    [Range(1, 100)]
    public int MetaLearningSteps { get; set; } = 5;

    /// <summary>Gets or sets meta-learning rate.</summary>
    [Range(0.0001, 0.1)]
    public double MetaLearningRate { get; set; } = 0.001;
}