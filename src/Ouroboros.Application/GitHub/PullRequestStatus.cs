namespace Ouroboros.Application.GitHub;

/// <summary>
/// Status information for a pull request.
/// </summary>
public sealed record PullRequestStatus
{
    /// <summary>
    /// Gets the pull request number.
    /// </summary>
    public required int Number { get; init; }

    /// <summary>
    /// Gets the state (open, closed, merged).
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// Gets whether the PR is mergeable.
    /// </summary>
    public bool? Mergeable { get; init; }

    /// <summary>
    /// Gets the number of additions.
    /// </summary>
    public required int Additions { get; init; }

    /// <summary>
    /// Gets the number of deletions.
    /// </summary>
    public required int Deletions { get; init; }

    /// <summary>
    /// Gets the number of changed files.
    /// </summary>
    public required int ChangedFiles { get; init; }
}