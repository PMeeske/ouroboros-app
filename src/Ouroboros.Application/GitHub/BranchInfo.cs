namespace Ouroboros.Application.GitHub;

/// <summary>
/// Information about a branch.
/// </summary>
public sealed record BranchInfo
{
    /// <summary>
    /// Gets the branch name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the commit SHA at the head of the branch.
    /// </summary>
    public required string Sha { get; init; }

    /// <summary>
    /// Gets whether the branch is protected.
    /// </summary>
    public required bool IsProtected { get; init; }
}