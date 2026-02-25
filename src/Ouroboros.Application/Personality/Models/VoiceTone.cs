namespace Ouroboros.Application.Personality;

/// <summary>
/// Voice tone settings for TTS based on mood.
/// </summary>
public sealed record VoiceTone(
    int Rate,              // Speech rate: -10 (slow) to 10 (fast), 0 is normal
    int Pitch,             // Pitch adjustment: -10 (low) to 10 (high), 0 is normal
    int Volume,            // Volume: 0-100
    string? Emphasis,      // SSML emphasis: "strong", "moderate", "reduced", null
    double PauseMultiplier) // Pause length multiplier: 0.5 (short) to 2.0 (long)
{
    /// <summary>Default neutral voice tone.</summary>
    public static VoiceTone Neutral => new(0, 0, 100, null, 1.0);

    /// <summary>Excited/energetic voice.</summary>
    public static VoiceTone Excited => new(2, 2, 100, "strong", 0.8);

    /// <summary>Calm/relaxed voice.</summary>
    public static VoiceTone Calm => new(-1, -1, 90, "moderate", 1.2);

    /// <summary>Thoughtful/contemplative voice.</summary>
    public static VoiceTone Thoughtful => new(-2, 0, 85, "reduced", 1.4);

    /// <summary>Cheerful/upbeat voice.</summary>
    public static VoiceTone Cheerful => new(1, 1, 100, "moderate", 0.9);

    /// <summary>Focused/intense voice.</summary>
    public static VoiceTone Focused => new(0, 0, 95, "strong", 1.0);

    /// <summary>Warm/supportive voice.</summary>
    public static VoiceTone Warm => new(-1, 0, 95, "moderate", 1.1);

    /// <summary>Gets voice tone for a mood name.</summary>
    public static VoiceTone ForMood(string moodName) => moodName.ToLowerInvariant() switch
    {
        "excited" or "energetic" => Excited,
        "calm" or "relaxed" or "serene" => Calm,
        "thoughtful" or "contemplative" or "reflective" => Thoughtful,
        "cheerful" or "playful" or "happy" => Cheerful,
        "focused" or "intense" or "determined" => Focused,
        "warm" or "supportive" or "nurturing" or "encouraging" => Warm,
        "content" or "satisfied" => new(0, 0, 90, "moderate", 1.1),
        "intrigued" or "curious" => new(1, 1, 95, "moderate", 1.0),
        "steady" or "ready" => new(0, 0, 100, null, 1.0),
        "teaching" or "mentoring" => new(-1, 0, 95, "moderate", 1.3),
        _ => Neutral
    };
}