namespace Ouroboros.Application.Integration;

/// <summary>Options for cognitive loop configuration.</summary>
public sealed record CognitiveLoopOptions(
    TimeSpan CycleInterval = default,
    bool AutoStart = false,
    int MaxCyclesPerRun = -1)
{
    /// <summary>Gets default cognitive loop options.</summary>
    public static CognitiveLoopOptions Default => new(
        CycleInterval: TimeSpan.FromSeconds(1));
}