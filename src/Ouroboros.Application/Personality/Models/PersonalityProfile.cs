namespace Ouroboros.Application.Personality;

/// <summary>
/// Complete personality profile that can evolve.
/// </summary>
public sealed record PersonalityProfile(
    string PersonaName,
    Dictionary<string, PersonalityTrait> Traits,
    MoodState CurrentMood,
    List<CuriosityDriver> CuriosityDrivers,
    string CoreIdentity,
    double AdaptabilityScore,
    int InteractionCount,
    DateTime LastEvolution)
{
    /// <summary>Gets the top active traits based on mood modulation.</summary>
    public IEnumerable<(string Name, double EffectiveIntensity)> GetActiveTraits(int count = 3)
    {
        return Traits
            .Select(t => (
                t.Key,
                EffectiveIntensity: t.Value.Intensity *
                                    (CurrentMood.TraitModifiers.TryGetValue(t.Key, out double mod) ? mod : 1.0)))
            .OrderByDescending(t => t.EffectiveIntensity)
            .Take(count);
    }
}