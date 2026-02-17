using System.ComponentModel.DataAnnotations;

namespace Ouroboros.Application.Integration;

/// <summary>Planning configuration.</summary>
public sealed class PlanningConfig
{
    /// <summary>Gets or sets maximum planning depth.</summary>
    [Range(1, 100)]
    public int MaxPlanningDepth { get; set; } = 10;

    /// <summary>Gets or sets maximum planning time in seconds.</summary>
    [Range(1, 300)]
    public int MaxPlanningTimeSeconds { get; set; } = 30;

    /// <summary>Gets or sets whether HTN is enabled.</summary>
    public bool EnableHTN { get; set; } = true;

    /// <summary>Gets or sets whether temporal planning is enabled.</summary>
    public bool EnableTemporalPlanning { get; set; } = true;
}