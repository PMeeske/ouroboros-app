namespace Ouroboros.Application.Personality;

/// <summary>
/// A personality state snapshot stored in Qdrant.
/// </summary>
public sealed record PersonalitySnapshot(
    Guid Id,
    string PersonaName,
    Dictionary<string, double> TraitIntensities,
    string CurrentMood,
    double AdaptabilityScore,
    int InteractionCount,
    DateTime Timestamp);