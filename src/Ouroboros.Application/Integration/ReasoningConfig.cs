namespace Ouroboros.Application.Integration;

/// <summary>
/// Configuration for reasoning operations.
/// </summary>
public sealed record ReasoningConfig(
    bool UseSymbolicReasoning = true,
    bool UseCausalInference = true,
    bool UseAbduction = true,
    int MaxInferenceSteps = 100)
{
    /// <summary>Gets the default reasoning configuration.</summary>
    public static ReasoningConfig Default => new();
}