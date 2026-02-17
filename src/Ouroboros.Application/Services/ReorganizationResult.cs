namespace Ouroboros.Application.Services;

/// <summary>
/// Result of a reorganization operation.
/// </summary>
public sealed record ReorganizationResult
{
    public int ClustersFound { get; init; }
    public int ConsolidatedChunks { get; init; }
    public int DuplicatesRemoved { get; init; }
    public int SummariesCreated { get; init; }
    public TimeSpan Duration { get; init; }
    public List<string> Insights { get; init; } = new();
}