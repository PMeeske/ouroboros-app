namespace Ouroboros.Application.Personality;

/// <summary>
/// Detected person profile based on communication patterns.
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
        Confidence: 0.0);
}