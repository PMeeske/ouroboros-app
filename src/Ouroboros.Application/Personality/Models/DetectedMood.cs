namespace Ouroboros.Application.Personality;

/// <summary>
/// Detected mood from user input with confidence scores.
/// </summary>
public sealed record DetectedMood(
    double Energy,           // -1 to 1: low energy to high energy
    double Positivity,       // -1 to 1: negative to positive
    double Urgency,          // 0 to 1: how urgent/time-sensitive
    double Curiosity,        // 0 to 1: inquisitive/exploratory
    double Frustration,      // 0 to 1: frustration level
    double Engagement,       // 0 to 1: how engaged/interested
    string? DominantEmotion, // Primary detected emotion
    double Confidence)       // Overall confidence in detection
{
    /// <summary>Creates a neutral detected mood.</summary>
    public static DetectedMood Neutral => new(0, 0, 0, 0, 0, 0.5, null, 0.5);
}