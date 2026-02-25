namespace Ouroboros.Application.Personality;

/// <summary>
/// Attentional gate that filters which stimuli reach consciousness.
/// Implements a simple model of selective attention.
/// </summary>
public sealed record AttentionalGate(
    double Threshold,            // Minimum salience to pass through
    double Capacity,             // 0-1: current attentional capacity
    string[] PrimedCategories,   // Categories that get attentional boost
    double FatigueFactor,        // Reduces capacity over time
    DateTime LastReset)
{
    /// <summary>Determines if a stimulus passes the attentional gate.</summary>
    public bool Allows(Stimulus stimulus)
    {
        double effectiveSalience = stimulus.Salience;

        // Boost for primed categories
        if (stimulus.Category != null && PrimedCategories.Contains(stimulus.Category))
            effectiveSalience *= 1.5;

        // Reduce threshold if capacity is high
        double effectiveThreshold = Threshold * (1.0 - Capacity * 0.3);

        return effectiveSalience >= effectiveThreshold;
    }

    /// <summary>Applies fatigue to capacity.</summary>
    public AttentionalGate ApplyFatigue(TimeSpan elapsed)
    {
        double fatigueAmount = FatigueFactor * elapsed.TotalMinutes;
        double newCapacity = Math.Max(0.1, Capacity - fatigueAmount);
        return this with { Capacity = newCapacity };
    }

    /// <summary>Resets attentional capacity (like after a break).</summary>
    public AttentionalGate Reset() =>
        this with { Capacity = 1.0, LastReset = DateTime.UtcNow };

    /// <summary>Creates a default attentional gate.</summary>
    public static AttentionalGate Default() => new(
        Threshold: 0.3,
        Capacity: 1.0,
        PrimedCategories: new[] { "social", "emotional", "novel" },
        FatigueFactor: 0.001,
        LastReset: DateTime.UtcNow);
}