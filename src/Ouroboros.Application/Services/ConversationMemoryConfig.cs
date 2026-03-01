using Ouroboros.Application.Configuration;

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
    [Obsolete("Use QdrantSettings from DI instead. This property is ignored when using the DI constructor.")]
    public string QdrantEndpoint { get; init; } = DefaultEndpoints.QdrantGrpc;

    /// <summary>Collection name for conversation embeddings.</summary>
    public string CollectionName { get; init; } = "ouroboros_conversations";

    /// <summary>Max turns to keep in active memory.</summary>
    public int MaxActiveTurns { get; init; } = 50;

    /// <summary>How many recent sessions to load on startup.</summary>
    public int RecentSessionsToLoad { get; init; } = 5;

    /// <summary>Vector dimensions.</summary>
    [Obsolete("Use QdrantSettings.DefaultVectorSize from DI instead.")]
    public int VectorSize { get; init; } = 768;
}