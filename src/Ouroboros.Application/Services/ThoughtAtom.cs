namespace Ouroboros.Application.Services;

/// <summary>
/// Represents a single thought atom from a stream.
/// </summary>
public record ThoughtAtom
{
    /// <summary>
    /// Gets the source stream ID.
    /// </summary>
    public required string StreamId { get; init; }

    /// <summary>
    /// Gets the atom content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets the atom type.
    /// </summary>
    public ThoughtAtomType Type { get; init; }

    /// <summary>
    /// Gets the timestamp.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the sequence number within the stream.
    /// </summary>
    public int SequenceNumber { get; init; }
}