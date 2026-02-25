using System.ComponentModel.DataAnnotations;

namespace Ouroboros.Application.Integration;

/// <summary>Reflection configuration.</summary>
public sealed class ReflectionConfig
{
    /// <summary>Gets or sets analysis window in hours.</summary>
    [Range(1, 168)]
    public int AnalysisWindowHours { get; set; } = 24;

    /// <summary>Gets or sets minimum episodes for analysis.</summary>
    [Range(1, 1000)]
    public int MinEpisodesForAnalysis { get; set; } = 10;

    /// <summary>Gets or sets whether auto-improvement is enabled.</summary>
    public bool EnableAutoImprovement { get; set; } = true;
}