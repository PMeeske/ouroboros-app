namespace Ouroboros.Application.Personality;

/// <summary>
/// Priority level for thought processing.
/// </summary>
public enum ThoughtPriority
{
    /// <summary>Background thought, process when idle.</summary>
    Background = 0,
    /// <summary>Low priority, can be deferred.</summary>
    Low = 1,
    /// <summary>Normal processing priority.</summary>
    Normal = 2,
    /// <summary>High priority, process soon.</summary>
    High = 3,
    /// <summary>Urgent, process immediately.</summary>
    Urgent = 4
}