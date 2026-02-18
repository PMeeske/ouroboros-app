namespace Ouroboros.ApiHost.Models;

/// <summary>
/// Response model for pipeline endpoint
/// </summary>
public sealed record PipelineResponse
{
    /// <summary>
    /// The final result text from the pipeline execution
    /// </summary>
    public required string Result { get; init; }

    /// <summary>
    /// The final state of the pipeline (optional)
    /// </summary>
    public string? FinalState { get; init; }
}