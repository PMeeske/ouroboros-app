namespace Ouroboros.Application.Integration;

/// <summary>Options for causal reasoning configuration.</summary>
public sealed record CausalReasoningOptions(
    bool EnableInterventions = true,
    bool EnableCounterfactuals = true,
    int MaxCausalDepth = 5);