namespace Ouroboros.Application.Personality;

/// <summary>Complete snapshot of a persona for replication.</summary>
public record PersonaSnapshot
{
    public required string PersonaId { get; init; }
    public required PersonaIdentity Identity { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required TimeSpan Uptime { get; init; }
    public required int InteractionCount { get; init; }
    public required ConsciousnessState ConsciousnessState { get; init; }
    public required SelfAwareness SelfAwareness { get; init; }
    public required List<MemoryFragment> ShortTermMemory { get; init; }
    public required Dictionary<string, object> StateData { get; init; }
    public required string Version { get; init; }
}