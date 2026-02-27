namespace Ouroboros.Application.Integration;

/// <summary>
/// Represents insights from metacognitive monitoring.
/// </summary>
public sealed record MetacognitiveInsights(
    IReadOnlyList<string> DetectedConflicts,
    IReadOnlyList<string> IdentifiedPatterns,
    IReadOnlyList<string> ReflectionOpportunities,
    double OverallCoherence,
    IReadOnlyDictionary<string, int> AttentionDistribution);