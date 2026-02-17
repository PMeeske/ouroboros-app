namespace Ouroboros.Application.Personality;

/// <summary>
/// Represents a composite thought created through convolution operations.
/// </summary>
public sealed record CompositeThought
{
    /// <summary>Unique identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Original thoughts that were combined.</summary>
    public required string[] SourceThoughts { get; init; }

    /// <summary>Relationship/operation used in composition.</summary>
    public required string Relationship { get; init; }

    /// <summary>The composite vector representation.</summary>
    public required float[] CompositeVector { get; init; }

    /// <summary>Dimension of the composite vector.</summary>
    public required int Dimension { get; init; }

    /// <summary>When this composite was created.</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>Additional metadata.</summary>
    public Dictionary<string, object>? Metadata { get; init; }
}