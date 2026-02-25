using Ouroboros.Domain.SelfModification;

namespace Ouroboros.Application.GitHub;

/// <summary>
/// Represents a proposal for self-modification.
/// </summary>
public sealed record SelfModificationProposal
{
    /// <summary>
    /// Gets the title of the modification.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the description of what will be changed.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the rationale for the modification.
    /// </summary>
    public required string Rationale { get; init; }

    /// <summary>
    /// Gets the list of file changes.
    /// </summary>
    public required IReadOnlyList<FileChange> Changes { get; init; }

    /// <summary>
    /// Gets the category of change.
    /// </summary>
    public required ChangeCategory Category { get; init; }

    /// <summary>
    /// Gets a value indicating whether to request human review.
    /// </summary>
    public bool RequestReview { get; init; } = true;
}