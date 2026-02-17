namespace Ouroboros.Application.Hyperon;

/// <summary>
/// Event from the Hyperon flow system.
/// </summary>
public sealed class HyperonFlowEvent
{
    public required HyperonFlowEventType EventType { get; init; }
    public required DateTime Timestamp { get; init; }
    public object? Data { get; init; }
}