namespace Ouroboros.Application.Personality;

/// <summary>
/// A response that can be triggered by stimuli.
/// </summary>
public sealed record Response(
    string Id,
    string Name,
    ResponseType Type,
    double Intensity,            // 0-1: strength of response
    string EmotionalTone,        // Primary emotional quality
    string[] BehavioralTendencies,  // What actions this response promotes
    string[] CognitivePatterns,     // Thought patterns associated with this response
    string? VoiceToneModifier)      // How to adjust voice tone
{
    /// <summary>
    /// Salience of the unconditioned stimulus (β in Rescorla-Wagner).
    /// Default: 0.5. Range: 0.0-1.0.
    /// </summary>
    public double Salience { get; init; } = 0.5;

    /// <summary>Creates a basic emotional response.</summary>
    public static Response CreateEmotional(string name, string emotionalTone, double intensity = 0.7, double salience = 0.5) => new(
        Id: Guid.NewGuid().ToString(),
        Name: name,
        Type: ResponseType.Emotional,
        Intensity: intensity,
        EmotionalTone: emotionalTone,
        BehavioralTendencies: Array.Empty<string>(),
        CognitivePatterns: Array.Empty<string>(),
        VoiceToneModifier: null)
    {
        Salience = salience
    };

    /// <summary>Creates a cognitive response with thought patterns.</summary>
    public static Response CreateCognitive(string name, string[] patterns, double intensity = 0.6, double salience = 0.5) => new(
        Id: Guid.NewGuid().ToString(),
        Name: name,
        Type: ResponseType.Cognitive,
        Intensity: intensity,
        EmotionalTone: "neutral",
        BehavioralTendencies: Array.Empty<string>(),
        CognitivePatterns: patterns,
        VoiceToneModifier: null)
    {
        Salience = salience
    };
}