namespace Ouroboros.Application.Personality;

/// <summary>
/// A conversation memory item stored in Qdrant for long-term recall.
/// </summary>
public sealed record ConversationMemory(
    Guid Id,
    string PersonaName,
    string UserMessage,
    string AssistantResponse,
    string? Topic,
    string? DetectedMood,
    double Significance,         // 0-1: how important this memory is
    string[] Keywords,
    DateTime Timestamp)
{
    /// <summary>Creates a searchable text representation.</summary>
    public string ToSearchText() =>
        $"User: {UserMessage}\nAssistant: {AssistantResponse}\nTopic: {Topic ?? "general"}\nMood: {DetectedMood ?? "neutral"}";
}