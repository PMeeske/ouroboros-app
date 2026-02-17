namespace Ouroboros.Application.GitHub;

/// <summary>
/// Result of a code search operation.
/// </summary>
public sealed record CodeSearchResult
{
    /// <summary>
    /// Gets the file path where the code was found.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the filename.
    /// </summary>
    public required string Filename { get; init; }

    /// <summary>
    /// Gets the matched content snippet.
    /// </summary>
    public required string MatchedContent { get; init; }

    /// <summary>
    /// Gets the line number where the match was found.
    /// </summary>
    public int? LineNumber { get; init; }
}