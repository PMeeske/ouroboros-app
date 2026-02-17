namespace Ouroboros.Application.Personality;

/// <summary>
/// Mood state that modulates trait expression and voice tone.
/// </summary>
public sealed record MoodState(
    string Name,
    double Energy,           // 0.0-1.0 energy level
    double Positivity,       // 0.0-1.0 positive vs negative
    Dictionary<string, double> TraitModifiers,  // Modifies trait intensities
    VoiceTone? Tone = null)  // Voice tone for TTS
{
    /// <summary>Creates a neutral mood.</summary>
    public static MoodState Neutral => new("neutral", 0.5, 0.5, new Dictionary<string, double>(), VoiceTone.Neutral);

    /// <summary>Gets the voice tone, defaulting based on mood name if not set.</summary>
    public VoiceTone GetVoiceTone() => Tone ?? VoiceTone.ForMood(Name);
}