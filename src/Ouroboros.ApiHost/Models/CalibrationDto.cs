namespace Ouroboros.ApiHost.Models;

/// <summary>
/// DTO for calibration metrics.
/// </summary>
public sealed record CalibrationDto
{
    /// <summary>
    /// Gets or sets the total forecasts.
    /// </summary>
    public int TotalForecasts { get; set; }

    /// <summary>
    /// Gets or sets the average confidence.
    /// </summary>
    public double AverageConfidence { get; set; }

    /// <summary>
    /// Gets or sets the average accuracy.
    /// </summary>
    public double AverageAccuracy { get; set; }

    /// <summary>
    /// Gets or sets the Brier score.
    /// </summary>
    public double BrierScore { get; set; }

    /// <summary>
    /// Gets or sets the calibration error.
    /// </summary>
    public double CalibrationError { get; set; }
}