namespace Ouroboros.Application.Integration;

/// <summary>
/// Represents the current state of the cognitive loop.
/// </summary>
public sealed record CognitiveState(
    bool IsRunning,
    int CyclesCompleted,
    DateTime LastCycleTime,
    string CurrentPhase,
    IReadOnlyList<string> RecentActions);