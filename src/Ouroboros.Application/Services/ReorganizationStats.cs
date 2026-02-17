namespace Ouroboros.Application.Services;

/// <summary>
/// Statistics about reorganization state.
/// </summary>
public sealed record ReorganizationStats
{
    public int TrackedPatterns { get; init; }
    public int HotContentCount { get; init; }
    public int CoAccessClusters { get; init; }
    public List<(string FilePath, int AccessCount)> TopAccessedFiles { get; init; } = new();
}