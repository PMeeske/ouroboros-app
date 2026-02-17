namespace Ouroboros.Application.Personality;

/// <summary>
/// A comprehensive snapshot of the AI's autonomous inner state.
/// </summary>
public sealed record AutonomousInnerState(
    string PersonaName,
    ConsciousnessState Consciousness,
    InnerDialogSession? LastDialogSession,
    List<InnerThought> BackgroundThoughts,
    List<InnerThought> PendingAutonomousThoughts,
    MoodState? CurrentMood,
    string[] ActiveTraits,
    DateTime Timestamp)
{
    /// <summary>Whether the AI has active autonomous thoughts.</summary>
    public bool HasAutonomousActivity =>
        PendingAutonomousThoughts.Count > 0 || BackgroundThoughts.Any(t => t.IsAutonomous);

    /// <summary>Gets the dominant autonomous thought type if any.</summary>
    public InnerThoughtType? DominantAutonomousType =>
        BackgroundThoughts
            .Where(t => t.IsAutonomous)
            .GroupBy(t => t.Type)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key;
}