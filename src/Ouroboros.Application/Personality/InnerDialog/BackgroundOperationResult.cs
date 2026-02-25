namespace Ouroboros.Application.Personality;

/// <summary>
/// Result of a background operation executed by a thought.
/// </summary>
public sealed record BackgroundOperationResult(
    string OperationType,
    string OperationName,
    bool Success,
    string? ResultSummary,
    object? Data,
    TimeSpan Duration,
    InnerThoughtType TriggeringThoughtType);