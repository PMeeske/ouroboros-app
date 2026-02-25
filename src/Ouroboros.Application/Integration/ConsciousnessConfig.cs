using System.ComponentModel.DataAnnotations;

namespace Ouroboros.Application.Integration;

/// <summary>Consciousness configuration.</summary>
public sealed class ConsciousnessConfig
{
    /// <summary>Gets or sets maximum workspace size.</summary>
    [Range(10, 1000)]
    public int MaxWorkspaceSize { get; set; } = 100;

    /// <summary>Gets or sets item lifetime in minutes.</summary>
    [Range(1, 1440)]
    public int ItemLifetimeMinutes { get; set; } = 60;

    /// <summary>Gets or sets whether metacognition is enabled.</summary>
    public bool EnableMetacognition { get; set; } = true;
}