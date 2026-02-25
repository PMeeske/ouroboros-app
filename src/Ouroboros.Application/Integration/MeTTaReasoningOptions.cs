namespace Ouroboros.Application.Integration;

/// <summary>Options for MeTTa reasoning configuration.</summary>
public sealed record MeTTaReasoningOptions(
    string HyperonPath = "",
    int MaxInferenceSteps = 100,
    double ConfidenceThreshold = 0.7);