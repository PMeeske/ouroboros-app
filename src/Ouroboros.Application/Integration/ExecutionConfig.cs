namespace Ouroboros.Application.Integration;

/// <summary>
/// Configuration for goal execution.
/// </summary>
public sealed record ExecutionConfig(
    bool UseEpisodicMemory = true,
    bool UseCausalReasoning = true,
    bool UseHierarchicalPlanning = true,
    bool UseWorldModel = false,
    int MaxPlanningDepth = 10,
    TimeSpan Timeout = default)
{
    /// <summary>Gets the default execution configuration.</summary>
    public static ExecutionConfig Default => new();
}