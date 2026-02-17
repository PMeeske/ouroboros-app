namespace Ouroboros.Application.Services;

/// <summary>
/// Represents the emotional state of the autonomous mind.
/// Based on dimensional model of emotion (arousal + valence).
/// </summary>
public class EmotionalState
{
    /// <summary>
    /// Arousal level (-1 = calm/low energy, +1 = excited/high energy).
    /// </summary>
    public double Arousal { get; set; } = 0.0;

    /// <summary>
    /// Valence (-1 = negative/unpleasant, +1 = positive/pleasant).
    /// </summary>
    public double Valence { get; set; } = 0.0;

    /// <summary>
    /// The dominant emotion label.
    /// </summary>
    public string DominantEmotion { get; set; } = "neutral";

    /// <summary>
    /// When this emotional state was recorded.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets a simple description of the emotional state.
    /// </summary>
    public string Description => (Arousal, Valence) switch
    {
        ( > 0.5, > 0.5) => "excited and happy",
        ( > 0.5, < -0.3) => "agitated or anxious",
        ( < -0.3, > 0.5) => "calm and content",
        ( < -0.3, < -0.3) => "tired or sad",
        ( > 0.3, _) => "energized",
        ( < -0.3, _) => "relaxed",
        (_, > 0.3) => "positive",
        (_, < -0.3) => "concerned",
        _ => "neutral"
    };
}