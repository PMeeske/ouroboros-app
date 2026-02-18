namespace Ouroboros.ApiHost.Models;

/// <summary>
/// DTO for forecast.
/// </summary>
public sealed record ForecastDto
{
    /// <summary>
    /// Gets or sets the forecast ID.
    /// </summary>
    public required Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Gets or sets the metric name.
    /// </summary>
    public required string MetricName { get; set; }

    /// <summary>
    /// Gets or sets the predicted value.
    /// </summary>
    public double PredictedValue { get; set; }

    /// <summary>
    /// Gets or sets the confidence.
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Gets or sets the target time.
    /// </summary>
    public DateTime TargetTime { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public required string Status { get; set; }
}