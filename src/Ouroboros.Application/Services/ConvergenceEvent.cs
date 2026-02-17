namespace Ouroboros.Application.Services;

/// <summary>
/// Represents a convergence event across streams.
/// </summary>
public record ConvergenceEvent
{
    /// <summary>Gets the timestamp.</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>Gets the atom that triggered convergence detection.</summary>
    public required ThoughtAtom TriggerAtom { get; init; }

    /// <summary>Gets the IDs of streams that converged.</summary>
    public required List<string> ConvergentStreams { get; init; }

    /// <summary>Gets the shared concept extracted.</summary>
    public required string SharedConcept { get; init; }
}