using System.ComponentModel.DataAnnotations;

namespace Ouroboros.Application.Integration;

/// <summary>Cognitive loop configuration settings.</summary>
public sealed class CognitiveLoopSettings
{
    /// <summary>Gets or sets cycle interval in milliseconds.</summary>
    [Range(100, 60000)]
    public int CycleIntervalMs { get; set; } = 1000;

    /// <summary>Gets or sets maximum concurrent goals.</summary>
    [Range(1, 100)]
    public int MaxConcurrentGoals { get; set; } = 5;

    /// <summary>Gets or sets whether autonomous learning is enabled.</summary>
    public bool EnableAutonomousLearning { get; set; } = true;
}