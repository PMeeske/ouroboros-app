namespace Ouroboros.Application.Personality;

/// <summary>
/// Parameters for how collective consciousness influences individual inner dialog.
/// </summary>
public sealed record ConsciousnessInfluence(
    double EmotionalBias,
    double ActivationLevel,
    double CoherenceMultiplier,
    string CurrentFocus,
    InnerThoughtType[] SuggestedThoughtTypes)
{
    /// <summary>
    /// Modulates thought confidence based on consciousness state.
    /// </summary>
    public double ModulateConfidence(double baseConfidence)
    {
        // Higher coherence = more confident thoughts
        // Higher activation = slightly more confident (engaged)
        return Math.Clamp(
            baseConfidence * CoherenceMultiplier * (0.9 + ActivationLevel * 0.2),
            0.0,
            1.0);
    }

    /// <summary>
    /// Modulates thought priority based on consciousness state.
    /// </summary>
    public ThoughtPriority ModulatePriority(ThoughtPriority basePriority)
    {
        // High activation elevates priority
        if (ActivationLevel > 0.8 && basePriority < ThoughtPriority.High)
            return basePriority + 1;

        // Low activation reduces priority
        if (ActivationLevel < 0.2 && basePriority > ThoughtPriority.Background)
            return basePriority - 1;

        return basePriority;
    }

    /// <summary>
    /// Checks if a thought type is currently suggested by the collective consciousness.
    /// </summary>
    public bool IsSuggestedType(InnerThoughtType type)
        => SuggestedThoughtTypes.Contains(type);
}