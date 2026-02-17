namespace Ouroboros.Application.Integration;

/// <summary>
/// Represents insights from metacognitive monitoring.
/// </summary>
public sealed record MetacognitiveInsights(
    List<string> DetectedConflicts,
    List<string> IdentifiedPatterns,
    List<string> ReflectionOpportunities,
    double OverallCoherence,
    Dictionary<string, int> AttentionDistribution);