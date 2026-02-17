namespace Ouroboros.Application.Personality;

/// <summary>
/// A memory trace that can undergo consolidation (becoming stronger over time/sleep).
/// </summary>
public sealed record MemoryTrace(
    string Id,
    string Content,
    double EncodingStrength,     // How well it was initially encoded
    double ConsolidationLevel,   // How consolidated (stable) the memory is
    bool IsConsolidated,         // Has undergone consolidation
    DateTime Encoded,
    DateTime? LastRetrieved,
    int RetrievalCount)
{
    /// <summary>Applies consolidation (like during sleep/rest).</summary>
    public MemoryTrace Consolidate()
    {
        double newConsolidation = Math.Min(1.0, ConsolidationLevel + 0.2);
        return this with
        {
            ConsolidationLevel = newConsolidation,
            IsConsolidated = newConsolidation > 0.7
        };
    }

    /// <summary>Records a retrieval event (strengthens memory).</summary>
    public MemoryTrace Retrieve()
    {
        double boost = 0.1 / (1 + RetrievalCount * 0.1); // Diminishing returns
        return this with
        {
            EncodingStrength = Math.Min(1.0, EncodingStrength + boost),
            LastRetrieved = DateTime.UtcNow,
            RetrievalCount = RetrievalCount + 1
        };
    }

    /// <summary>Creates a new memory trace.</summary>
    public static MemoryTrace Create(string content, double encodingStrength = 0.6) => new(
        Id: Guid.NewGuid().ToString(),
        Content: content,
        EncodingStrength: encodingStrength,
        ConsolidationLevel: 0.1,
        IsConsolidated: false,
        Encoded: DateTime.UtcNow,
        LastRetrieved: null,
        RetrievalCount: 0);
}