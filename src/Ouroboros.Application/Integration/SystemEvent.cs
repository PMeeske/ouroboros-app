namespace Ouroboros.Application.Integration;

/// <summary>
/// Base event type for system-wide events.
/// </summary>
public abstract record SystemEvent(
    Guid EventId,
    DateTime Timestamp,
    string Source);