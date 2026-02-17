namespace Ouroboros.Application.Services;

/// <summary>
/// Result of a presence check.
/// </summary>
public record PresenceCheckResult
{
    /// <summary>When the check was performed.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Whether user is considered present.</summary>
    public bool IsPresent { get; set; }

    /// <summary>Overall confidence (0-1).</summary>
    public double OverallConfidence { get; set; }

    /// <summary>Number of WiFi devices detected nearby.</summary>
    public int WifiDevicesNearby { get; set; }

    /// <summary>WiFi-based presence confidence.</summary>
    public double WifiPresenceConfidence { get; set; }

    /// <summary>Whether motion was detected via camera.</summary>
    public bool MotionDetected { get; set; }

    /// <summary>Camera-based presence confidence.</summary>
    public double CameraConfidence { get; set; }

    /// <summary>Whether recent input activity detected.</summary>
    public bool RecentInputActivity { get; set; }

    /// <summary>Input activity confidence.</summary>
    public double InputActivityConfidence { get; set; }
}