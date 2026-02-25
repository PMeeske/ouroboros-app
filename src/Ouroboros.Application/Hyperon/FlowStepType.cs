namespace Ouroboros.Application.Hyperon;

/// <summary>
/// Types of flow steps.
/// </summary>
public enum FlowStepType
{
    LoadFacts,
    ApplyRule,
    Query,
    Transform,
    Filter,
    SideEffect
}