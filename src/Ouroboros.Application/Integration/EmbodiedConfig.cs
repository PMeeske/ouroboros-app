using System.ComponentModel.DataAnnotations;

namespace Ouroboros.Application.Integration;

/// <summary>Embodied simulation configuration.</summary>
public sealed class EmbodiedConfig
{
    /// <summary>Gets or sets environment name.</summary>
    [Required]
    public string Environment { get; set; } = "gym";

    /// <summary>Gets or sets whether physics simulation is enabled.</summary>
    public bool EnablePhysicsSimulation { get; set; } = true;

    /// <summary>Gets or sets maximum simulation steps.</summary>
    [Range(100, 100000)]
    public int MaxSimulationSteps { get; set; } = 1000;
}