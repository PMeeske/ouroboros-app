namespace Ouroboros.Application.GitHub;

/// <summary>
/// Information about file content from GitHub.
/// </summary>
public sealed record FileContent
{
    /// <summary>
    /// Gets the file path.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the file content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets the file size in bytes.
    /// </summary>
    public required int Size { get; init; }

    /// <summary>
    /// Gets the SHA of the file.
    /// </summary>
    public required string Sha { get; init; }
}