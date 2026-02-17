namespace Ouroboros.WebApi.Models;

/// <summary>
/// Response containing agent identity state.
/// </summary>
public sealed record SelfStateResponse
{
    /// <summary>
    /// Gets or sets the agent ID.
    /// </summary>
    public required Guid AgentId { get; set; }

    /// <summary>
    /// Gets or sets the agent name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the number of capabilities.
    /// </summary>
    public int CapabilityCount { get; set; }

    /// <summary>
    /// Gets or sets the available resources.
    /// </summary>
    public Dictionary<string, object>? Resources { get; set; }

    /// <summary>
    /// Gets or sets the active commitments.
    /// </summary>
    public List<CommitmentDto>? Commitments { get; set; }

    /// <summary>
    /// Gets or sets the performance metrics.
    /// </summary>
    public PerformanceDto? Performance { get; set; }

    /// <summary>
    /// Gets or sets the state timestamp.
    /// </summary>
    public DateTime StateTimestamp { get; set; }
}