namespace Ouroboros.Application.Personality;

/// <summary>A fragment of memory from an interaction.</summary>
public record MemoryFragment
{
    public DateTime Timestamp { get; init; }
    public required string UserInput { get; init; }
    public required string Response { get; init; }
    public required string Summary { get; init; }
    public required string EmotionalContext { get; init; }
}