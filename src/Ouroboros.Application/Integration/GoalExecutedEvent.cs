namespace Ouroboros.Application.Integration;

/// <summary>
/// Event fired when a goal is executed.
/// </summary>
public sealed record GoalExecutedEvent(
    Guid EventId,
    DateTime Timestamp,
    string Source,
    string Goal,
    bool Success,
    TimeSpan Duration) : SystemEvent(EventId, Timestamp, Source);