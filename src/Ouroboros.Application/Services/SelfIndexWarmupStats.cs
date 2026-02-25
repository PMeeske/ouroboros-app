namespace Ouroboros.Application.Services;

/// <summary>
/// Statistics from self-index warmup.
/// </summary>
public record SelfIndexWarmupStats
{
    /// <summary>Total vectors in the index.</summary>
    public long TotalVectors { get; set; }

    /// <summary>Number of indexed files.</summary>
    public int IndexedFiles { get; set; }

    /// <summary>Collection name in Qdrant.</summary>
    public string? CollectionName { get; set; }

    /// <summary>Number of search queries tested.</summary>
    public int SearchQueriesTested { get; set; }

    /// <summary>Number of successful search queries.</summary>
    public int SearchQueriesSucceeded { get; set; }

    /// <summary>Number of tracked access patterns.</summary>
    public int TrackedPatterns { get; set; }

    /// <summary>Number of hot (frequently accessed) content items.</summary>
    public int HotContentCount { get; set; }

    /// <summary>Number of co-access clusters identified.</summary>
    public int CoAccessClusters { get; set; }

    /// <summary>Whether reorganization was triggered.</summary>
    public bool ReorganizationTriggered { get; set; }

    /// <summary>Number of chunks reorganized during warmup.</summary>
    public int ReorganizedChunks { get; set; }
}