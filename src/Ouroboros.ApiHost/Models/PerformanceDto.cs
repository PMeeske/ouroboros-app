namespace Ouroboros.ApiHost.Models;

/// <summary>
/// DTO for performance metrics.
/// </summary>
public sealed record PerformanceDto
{
    /// <summary>
    /// Gets or sets the overall success rate.
    /// </summary>
    public double OverallSuccessRate { get; set; }

    /// <summary>
    /// Gets or sets the average response time in milliseconds.
    /// </summary>
    public double AverageResponseTime { get; set; }

    /// <summary>
    /// Gets or sets the total tasks.
    /// </summary>
    public int TotalTasks { get; set; }

    /// <summary>
    /// Gets or sets the successful tasks.
    /// </summary>
    public int SuccessfulTasks { get; set; }

    /// <summary>
    /// Gets or sets the failed tasks.
    /// </summary>
    public int FailedTasks { get; set; }
}