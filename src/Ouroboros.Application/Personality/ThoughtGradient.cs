namespace Ouroboros.Application.Personality;

/// <summary>
/// Represents the gradient/transition between two thoughts.
/// </summary>
public sealed record ThoughtGradient
{
    /// <summary>Starting thought.</summary>
    public required string FromThought { get; init; }

    /// <summary>Ending thought.</summary>
    public required string ToThought { get; init; }

    /// <summary>Gradient vector (direction of change).</summary>
    public required float[] GradientVector { get; init; }

    /// <summary>Cosine similarity between thoughts.</summary>
    public required float Similarity { get; init; }

    /// <summary>Magnitude of the transition.</summary>
    public required float TransitionMagnitude { get; init; }
}