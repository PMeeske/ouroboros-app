namespace Ouroboros.Application.Integration;

/// <summary>
/// Configuration for the cognitive loop.
/// </summary>
public sealed record CognitiveLoopConfig(
    TimeSpan CycleInterval = default,
    bool EnablePerception = true,
    bool EnableReasoning = true,
    bool EnableAction = true,
    int MaxCyclesPerRun = -1,
    double AttentionThreshold = 0.5)
{
    /// <summary>Gets the default cognitive loop configuration.</summary>
    public static CognitiveLoopConfig Default => new(TimeSpan.FromSeconds(1));
}