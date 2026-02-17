namespace Ouroboros.Application.Integration;

/// <summary>
/// Event fired when reasoning completes.
/// </summary>
public sealed record ReasoningCompletedEvent(
    Guid EventId,
    DateTime Timestamp,
    string Source,
    string Query,
    string Answer,
    double Confidence) : SystemEvent(EventId, Timestamp, Source);