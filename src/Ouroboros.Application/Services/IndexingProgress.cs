namespace Ouroboros.Application.Services;

/// <summary>
/// Progress information for indexing operations.
/// </summary>
public sealed record IndexingProgress
{
    public int TotalFiles { get; init; }
    public int ProcessedFiles { get; init; }
    public int IndexedChunks { get; init; }
    public int SkippedFiles { get; init; }
    public int ErrorFiles { get; init; }
    public string CurrentFile { get; init; } = string.Empty;
    public TimeSpan Elapsed { get; init; }
    public bool IsComplete { get; init; }
    public string? Error { get; init; }
}