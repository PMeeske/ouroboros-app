namespace Ouroboros.Application.GitHub;

/// <summary>
/// Information about a pull request.
/// </summary>
public sealed record PullRequestInfo
{
    /// <summary>
    /// Gets the pull request number.
    /// </summary>
    public required int Number { get; init; }

    /// <summary>
    /// Gets the URL of the pull request.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets the title of the pull request.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the state of the pull request (open, closed, merged).
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// Gets the head branch name.
    /// </summary>
    public required string HeadBranch { get; init; }

    /// <summary>
    /// Gets the base branch name.
    /// </summary>
    public required string BaseBranch { get; init; }

    /// <summary>
    /// Gets when the pull request was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }
}