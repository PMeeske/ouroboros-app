namespace Ouroboros.Application.Services;

/// <summary>
/// Search result from the self-index.
/// </summary>
public sealed record SearchResult
{
    public string FilePath { get; init; } = string.Empty;
    public int ChunkIndex { get; init; }
    public string Content { get; init; } = string.Empty;
    public float Score { get; init; }
}