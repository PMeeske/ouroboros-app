namespace Ouroboros.WebApi.Models;

/// <summary>
/// DTO for anomaly.
/// </summary>
public sealed record AnomalyDto
{
    /// <summary>
    /// Gets or sets the metric name.
    /// </summary>
    public required string MetricName { get; set; }

    /// <summary>
    /// Gets or sets the observed value.
    /// </summary>
    public double ObservedValue { get; set; }

    /// <summary>
    /// Gets or sets the expected value.
    /// </summary>
    public double ExpectedValue { get; set; }

    /// <summary>
    /// Gets or sets the deviation.
    /// </summary>
    public double Deviation { get; set; }

    /// <summary>
    /// Gets or sets the severity.
    /// </summary>
    public required string Severity { get; set; }

    /// <summary>
    /// Gets or sets the detected timestamp.
    /// </summary>
    public DateTime DetectedAt { get; set; }
}