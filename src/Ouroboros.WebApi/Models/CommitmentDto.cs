namespace Ouroboros.WebApi.Models;

/// <summary>
/// DTO for agent commitment.
/// </summary>
public sealed record CommitmentDto
{
    /// <summary>
    /// Gets or sets the commitment ID.
    /// </summary>
    public required Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Gets or sets the deadline.
    /// </summary>
    public DateTime Deadline { get; set; }

    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    public double Priority { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    /// Gets or sets the progress percentage.
    /// </summary>
    public double ProgressPercent { get; set; }
}