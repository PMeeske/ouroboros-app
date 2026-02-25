namespace Ouroboros.Application.Services;

/// <summary>
/// Details about a single stream.
/// </summary>
public record StreamDetail
{
    /// <summary>Gets the stream ID.</summary>
    public required string StreamId { get; init; }

    /// <summary>Gets the atom count.</summary>
    public int AtomCount { get; init; }

    /// <summary>Gets the last activity time.</summary>
    public DateTime LastActivity { get; init; }
}