namespace Ouroboros.Application.Integration;

/// <summary>
/// Event fired when consciousness state changes.
/// </summary>
public sealed record ConsciousnessStateChangedEvent(
    Guid EventId,
    DateTime Timestamp,
    string Source,
    string NewState,
    List<string> ActiveItems) : SystemEvent(EventId, Timestamp, Source);