namespace Ouroboros.Application.Services;

/// <summary>
/// Configuration for presence detection.
/// </summary>
public record PresenceConfig
{
    /// <summary>Interval between presence checks in seconds.</summary>
    public int CheckIntervalSeconds { get; init; } = 5;

    /// <summary>Confidence threshold to consider user present (0-1).</summary>
    public double PresenceThreshold { get; init; } = 0.6;

    /// <summary>Number of consecutive frames needed to confirm presence.</summary>
    public int PresenceConfirmationFrames { get; init; } = 2;

    /// <summary>Number of consecutive frames needed to confirm absence.</summary>
    public int AbsenceConfirmationFrames { get; init; } = 6;

    /// <summary>Whether to use WiFi/network for detection.</summary>
    public bool UseWifi { get; init; } = true;

    /// <summary>Whether to use camera for detection.</summary>
    public bool UseCamera { get; init; } = false; // Disabled by default for privacy

    /// <summary>Whether to use keyboard/mouse activity for detection.</summary>
    public bool UseInputActivity { get; init; } = true;

    /// <summary>Seconds of input idle before considering inactive.</summary>
    public int InputIdleThresholdSeconds { get; init; } = 300; // 5 minutes
}