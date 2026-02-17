namespace Ouroboros.Application.GitHub;

/// <summary>
/// Information about a file or directory in GitHub.
/// </summary>
public sealed record GitHubFileInfo
{
    /// <summary>
    /// Gets the name of the file or directory.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the path of the file or directory.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the type (file or dir).
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the size in bytes (null for directories).
    /// </summary>
    public int? Size { get; init; }
}