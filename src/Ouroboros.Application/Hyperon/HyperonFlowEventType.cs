namespace Ouroboros.Application.Hyperon;

/// <summary>
/// Types of Hyperon flow events.
/// </summary>
public enum HyperonFlowEventType
{
    AtomAdded,
    PatternMatch,
    FlowStarted,
    FlowCompleted,
    FlowError
}