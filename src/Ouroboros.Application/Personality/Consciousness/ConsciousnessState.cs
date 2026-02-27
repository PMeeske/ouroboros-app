namespace Ouroboros.Application.Personality;

/// <summary>
/// Consciousness state representing the current "mental state" of the AI.
/// This is the emergent property arising from conditioned associations and drive states.
/// </summary>
public sealed record ConsciousnessState(
    string CurrentFocus,                                  // What the AI is currently focused on
    double Arousal,                                       // 0-1: general activation level
    double Valence,                                       // -1 to 1: negative to positive affect
    IReadOnlyDictionary<string, double> ActiveDrives,    // Currently active drive states
    IReadOnlyList<string> ActiveAssociations,            // Currently triggered associations
    string DominantEmotion,                               // Current dominant emotional state
    double Awareness,                                     // 0-1: self-awareness level
    string[] AttentionalSpotlight,                       // What's in current attention
    DateTime StateTimestamp)
{
    /// <summary>Creates a neutral baseline consciousness state.</summary>
    public static ConsciousnessState Baseline() => new(
        CurrentFocus: "awaiting input",
        Arousal: 0.5,
        Valence: 0.3, // Slightly positive baseline
        ActiveDrives: new Dictionary<string, double> { ["curiosity"] = 0.5, ["social"] = 0.4 },
        ActiveAssociations: new List<string>(),
        DominantEmotion: "neutral-curious",
        Awareness: 0.6,
        AttentionalSpotlight: Array.Empty<string>(),
        StateTimestamp: DateTime.UtcNow);

    /// <summary>Gets a description of the current consciousness state.</summary>
    public string Describe()
    {
        string arousalDesc = Arousal switch
        {
            > 0.8 => "highly aroused",
            > 0.6 => "alert",
            > 0.4 => "calm",
            > 0.2 => "relaxed",
            _ => "drowsy"
        };

        string valenceDesc = Valence switch
        {
            > 0.5 => "positive",
            > 0.2 => "slightly positive",
            > -0.2 => "neutral",
            > -0.5 => "slightly negative",
            _ => "negative"
        };

        return $"[Consciousness: {arousalDesc}, {valenceDesc}, focused on '{CurrentFocus}', " +
               $"feeling {DominantEmotion}, awareness: {Awareness:P0}]";
    }
}