namespace Ouroboros.Application.Services;

/// <summary>
/// Configuration for conversation memory.
/// </summary>
public sealed record ConversationMemoryConfig
{
    /// <summary>Directory for storing conversation files.</summary>
    public string StorageDirectory { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".ouroboros", "conversations");

    /// <summary>Qdrant endpoint for semantic memory.</summary>
    public string QdrantEndpoint { get; init; } = "http://localhost:6334";

    /// <summary>Collection name for conversation embeddings.</summary>
    public string CollectionName { get; init; } = "ouroboros_conversations";

    /// <summary>Max turns to keep in active memory.</summary>
    public int MaxActiveTurns { get; init; } = 50;

    /// <summary>How many recent sessions to load on startup.</summary>
    public int RecentSessionsToLoad { get; init; } = 5;

    /// <summary>Vector dimensions.</summary>
    public int VectorSize { get; init; } = 768;
}