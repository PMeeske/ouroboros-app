namespace Ouroboros.Application.Services;

/// <summary>
/// Statistics about parallel streams.
/// </summary>
public record ParallelStreamStats
{
    /// <summary>Gets the number of active streams.</summary>
    public int ActiveStreams { get; init; }

    /// <summary>Gets the total atoms generated.</summary>
    public int TotalAtomsGenerated { get; init; }

    /// <summary>Gets the convergence event count.</summary>
    public int ConvergenceEvents { get; init; }

    /// <summary>Gets per-stream details.</summary>
    public required List<StreamDetail> StreamDetails { get; init; }
}