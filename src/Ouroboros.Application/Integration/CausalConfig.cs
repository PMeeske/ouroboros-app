using System.ComponentModel.DataAnnotations;

namespace Ouroboros.Application.Integration;

/// <summary>Causal reasoning configuration.</summary>
public sealed class CausalConfig
{
    /// <summary>Gets or sets discovery algorithm.</summary>
    [Required]
    public string DiscoveryAlgorithm { get; set; } = "PC";

    /// <summary>Gets or sets maximum causal complexity.</summary>
    [Range(10, 1000)]
    public int MaxCausalComplexity { get; set; } = 100;

    /// <summary>Gets or sets whether counterfactuals are enabled.</summary>
    public bool EnableCounterfactuals { get; set; } = true;
}