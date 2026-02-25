namespace Ouroboros.Application.Services;

/// <summary>
/// Presence event data.
/// </summary>
public record PresenceEvent
{
    /// <summary>The presence state.</summary>
    public required PresenceState State { get; init; }

    /// <summary>When the event occurred.</summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>Confidence level (0-1).</summary>
    public double Confidence { get; init; }

    /// <summary>Detection source (wifi, camera, input, etc.).</summary>
    public string Source { get; init; } = "";

    /// <summary>Time since last state change.</summary>
    public TimeSpan? TimeSinceLastState { get; init; }
}