using System.ComponentModel.DataAnnotations;

namespace Ouroboros.Application.Integration;

/// <summary>MeTTa reasoning configuration.</summary>
public sealed class MeTTaConfig
{
    /// <summary>Gets or sets MeTTa executable path.</summary>
    [Required]
    public string ExecutablePath { get; set; } = "./metta";

    /// <summary>Gets or sets maximum inference steps.</summary>
    [Range(10, 10000)]
    public int MaxInferenceSteps { get; set; } = 100;

    /// <summary>Gets or sets whether type checking is enabled.</summary>
    public bool EnableTypeChecking { get; set; } = true;

    /// <summary>Gets or sets whether abduction is enabled.</summary>
    public bool EnableAbduction { get; set; } = true;
}