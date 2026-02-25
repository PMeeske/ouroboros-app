namespace Ouroboros.Application.Integration;

/// <summary>Options for hierarchical planning configuration.</summary>
public sealed record HierarchicalPlanningOptions(
    int MaxDepth = 10,
    int MinStepsForDecomposition = 3,
    double ComplexityThreshold = 0.7);