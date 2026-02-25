namespace Ouroboros.Application.Personality.Consciousness;

/// <summary>
/// Statistics about cognitive processing.
/// </summary>
public sealed record CognitiveProcessingStats(
    int TotalWorkspaceItems,
    int ConsciousExperiencesInWorkspace,
    double CurrentArousal,
    double CurrentValence,
    double CurrentAwareness,
    int ActiveAssociations,
    double WorkspaceAverageAttention);