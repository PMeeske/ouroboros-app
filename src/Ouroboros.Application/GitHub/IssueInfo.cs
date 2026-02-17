namespace Ouroboros.Application.GitHub;

/// <summary>
/// Information about an issue.
/// </summary>
public sealed record IssueInfo
{
    /// <summary>
    /// Gets the issue number.
    /// </summary>
    public required int Number { get; init; }

    /// <summary>
    /// Gets the URL of the issue.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets the title of the issue.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the state of the issue (open or closed).
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// Gets when the issue was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }
}