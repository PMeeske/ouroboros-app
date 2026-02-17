namespace Ouroboros.Application.Services;

/// <summary>
/// Statistics about the index.
/// </summary>
public sealed record IndexStats
{
    public long TotalVectors { get; init; }
    public int IndexedFiles { get; init; }
    public string CollectionName { get; init; } = string.Empty;
    public int VectorSize { get; init; }
}