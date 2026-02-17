using System.ComponentModel.DataAnnotations;

namespace Ouroboros.Application.Integration;

/// <summary>World model configuration.</summary>
public sealed class WorldModelConfig
{
    /// <summary>Gets or sets model architecture.</summary>
    [Required]
    public string ModelArchitecture { get; set; } = "transformer";

    /// <summary>Gets or sets maximum model complexity.</summary>
    [Range(1000, 10000000)]
    public int MaxModelComplexity { get; set; } = 1000000;

    /// <summary>Gets or sets whether imagination planning is enabled.</summary>
    public bool EnableImaginationPlanning { get; set; } = true;
}