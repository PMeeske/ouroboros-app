namespace Ouroboros.Application.Integration;

/// <summary>
/// Event fired when consciousness state changes.
/// </summary>
public sealed record ConsciousnessStateChangedEvent(
    Guid EventId,
    DateTime Timestamp,
    string Source,
    string NewState,
    IReadOnlyList<string> ActiveItems) : SystemEvent(EventId, Timestamp, Source);