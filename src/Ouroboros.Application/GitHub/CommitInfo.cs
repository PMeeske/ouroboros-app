namespace Ouroboros.Application.GitHub;

/// <summary>
/// Information about a commit.
/// </summary>
public sealed record CommitInfo
{
    /// <summary>
    /// Gets the commit SHA.
    /// </summary>
    public required string Sha { get; init; }

    /// <summary>
    /// Gets the commit message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the URL of the commit.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets when the commit was made.
    /// </summary>
    public required DateTime CommittedAt { get; init; }
}