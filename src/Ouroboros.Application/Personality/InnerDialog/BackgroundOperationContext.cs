namespace Ouroboros.Application.Personality;

/// <summary>
/// Context for background operations, containing conversation state and available resources.
/// </summary>
public sealed record BackgroundOperationContext(
    string? CurrentTopic,
    string? LastUserMessage,
    PersonalityProfile? Profile,
    SelfAwareness? SelfAwareness,
    List<string> RecentTopics,
    List<string> AvailableTools,
    List<string> AvailableSkills,
    Dictionary<string, object> ConversationMetadata)
{
    /// <summary>
    /// Extracts key concepts from the current topic and recent messages.
    /// </summary>
    public List<string> ExtractKeywords()
    {
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // From current topic
        if (!string.IsNullOrWhiteSpace(CurrentTopic))
        {
            foreach (var word in CurrentTopic.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (word.Length > 3) keywords.Add(word.ToLowerInvariant());
            }
        }

        // From last message
        if (!string.IsNullOrWhiteSpace(LastUserMessage))
        {
            foreach (var word in LastUserMessage.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (word.Length > 3) keywords.Add(word.ToLowerInvariant());
            }
        }

        // From recent topics
        foreach (var topic in RecentTopics.TakeLast(3))
        {
            foreach (var word in topic.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (word.Length > 3) keywords.Add(word.ToLowerInvariant());
            }
        }

        return keywords.ToList();
    }
}