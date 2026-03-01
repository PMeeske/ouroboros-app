namespace Ouroboros.Application.Integration;

/// <summary>
/// Represents the outcome of a single cognitive cycle.
/// </summary>
public sealed record CycleOutcome(
    bool Success,
    string Phase,
    TimeSpan Duration,
    IReadOnlyList<string> ActionsPerformed,
    IReadOnlyDictionary<string, object> Metrics);