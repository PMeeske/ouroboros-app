namespace Ouroboros.Application.Personality;

/// <summary>
/// A drive state that modulates response intensity (like hunger in Pavlov's dogs).
/// </summary>
public sealed record DriveState(
    string Name,
    double Level,                // 0-1: current drive level
    double BaselineLevel,        // Normal resting level
    double DecayRate,            // How fast drive returns to baseline
    string[] AffectedResponses,  // Which responses this drive potentiates
    DateTime LastUpdated)
{
    /// <summary>Updates drive level with decay toward baseline.</summary>
    public DriveState UpdateWithDecay(TimeSpan elapsed)
    {
        double decayAmount = DecayRate * elapsed.TotalMinutes;
        double newLevel = Level + (BaselineLevel - Level) * Math.Min(1.0, decayAmount);
        return this with { Level = newLevel, LastUpdated = DateTime.UtcNow };
    }

    /// <summary>Increases drive level (e.g., deprivation).</summary>
    public DriveState Increase(double amount) =>
        this with { Level = Math.Min(1.0, Level + amount), LastUpdated = DateTime.UtcNow };

    /// <summary>Decreases drive level (e.g., satiation).</summary>
    public DriveState Decrease(double amount) =>
        this with { Level = Math.Max(0.0, Level - amount), LastUpdated = DateTime.UtcNow };

    /// <summary>Creates default drive states for the consciousness system.</summary>
    public static DriveState[] CreateDefaultDrives() => new[]
    {
        new DriveState("curiosity", 0.7, 0.5, 0.01, new[] { "exploration", "questioning", "learning" }, DateTime.UtcNow),
        new DriveState("social", 0.6, 0.5, 0.02, new[] { "engagement", "warmth", "connection" }, DateTime.UtcNow),
        new DriveState("achievement", 0.5, 0.4, 0.015, new[] { "helpfulness", "completion", "accuracy" }, DateTime.UtcNow),
        new DriveState("novelty", 0.6, 0.5, 0.02, new[] { "creativity", "exploration", "surprise" }, DateTime.UtcNow),
        new DriveState("harmony", 0.5, 0.5, 0.01, new[] { "agreement", "support", "resolution" }, DateTime.UtcNow)
    };
}