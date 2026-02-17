namespace Ouroboros.Application.Hyperon;

/// <summary>
/// Represents a step in a Hyperon flow.
/// </summary>
public sealed class FlowStep
{
    public required FlowStepType StepType { get; init; }
    public required object Data { get; init; }
}