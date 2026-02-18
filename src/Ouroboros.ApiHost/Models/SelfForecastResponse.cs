namespace Ouroboros.ApiHost.Models;

/// <summary>
/// Response containing forecast information.
/// </summary>
public sealed record SelfForecastResponse
{
    /// <summary>
    /// Gets or sets the pending forecasts.
    /// </summary>
    public List<ForecastDto>? PendingForecasts { get; set; }

    /// <summary>
    /// Gets or sets the forecast calibration.
    /// </summary>
    public CalibrationDto? Calibration { get; set; }

    /// <summary>
    /// Gets or sets the recent anomalies.
    /// </summary>
    public List<AnomalyDto>? RecentAnomalies { get; set; }
}