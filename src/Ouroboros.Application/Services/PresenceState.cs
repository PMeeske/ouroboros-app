namespace Ouroboros.Application.Services;

/// <summary>
/// Presence state enumeration.
/// </summary>
public enum PresenceState
{
    /// <summary>Unknown presence state.</summary>
    Unknown,

    /// <summary>User is present.</summary>
    Present,

    /// <summary>User is absent.</summary>
    Absent,
}