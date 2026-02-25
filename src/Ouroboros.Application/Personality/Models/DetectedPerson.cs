namespace Ouroboros.Application.Personality;

/// <summary>
/// Detected person profile based on communication patterns and voice biometrics.
/// </summary>
public sealed record DetectedPerson(
    string Id,
    string? Name,                    // Explicitly stated name, if any
    string[] NameAliases,            // Alternative names/nicknames detected
    CommunicationStyle Style,        // Communication style fingerprint
    Dictionary<string, double> TopicInterests,  // Topics they frequently discuss
    string[] CommonPhrases,          // Distinctive phrases they use
    double VocabularyComplexity,     // 0-1: simple to complex vocabulary
    double Formality,                // 0-1: casual to formal
    int InteractionCount,
    DateTime FirstSeen,
    DateTime LastSeen,
    double? VoiceZeroCrossRate,      // Average zero-crossing rate (pitch proxy)
    double? VoiceSpeakingRate,       // Average speaking rate (words/sec)
    double? VoiceDynamicRange,       // Average dynamic range (expressiveness)
    double Confidence)               // 0-1: confidence in identification
{
    /// <summary>Creates a new unknown person.</summary>
    public static DetectedPerson Unknown() => new(
        Id: Guid.NewGuid().ToString(),
        Name: null,
        NameAliases: Array.Empty<string>(),
        Style: CommunicationStyle.Default,
        TopicInterests: new Dictionary<string, double>(),
        CommonPhrases: Array.Empty<string>(),
        VocabularyComplexity: 0.5,
        Formality: 0.5,
        InteractionCount: 1,
        FirstSeen: DateTime.UtcNow,
        LastSeen: DateTime.UtcNow,
        VoiceZeroCrossRate: null,
        VoiceSpeakingRate: null,
        VoiceDynamicRange: null,
        Confidence: 0.0);

    /// <summary>
    /// Computes voice similarity to the given signature values.
    /// Returns 0 if this person has no stored voice data.
    /// </summary>
    public double VoiceSimilarityTo(double zeroCrossRate, double speakingRate, double dynamicRange)
    {
        if (VoiceZeroCrossRate is not { } zcr ||
            VoiceSpeakingRate is not { } sr ||
            VoiceDynamicRange is not { } dr)
            return 0;

        // Normalise to [0,1] ranges (same as VoiceSignature.SimilarityTo)
        double a1 = zcr / 4000.0, a2 = sr / 5.0, a3 = dr;
        double b1 = zeroCrossRate / 4000.0, b2 = speakingRate / 5.0, b3 = dynamicRange;

        double dot   = a1 * b1 + a2 * b2 + a3 * b3;
        double normA = Math.Sqrt(a1 * a1 + a2 * a2 + a3 * a3);
        double normB = Math.Sqrt(b1 * b1 + b2 * b2 + b3 * b3);

        if (normA < 1e-9 || normB < 1e-9) return 0;
        return dot / (normA * normB);
    }
}
