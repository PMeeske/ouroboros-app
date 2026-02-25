namespace Ouroboros.Application.Services;

/// <summary>
/// Statistics about conversation memory.
/// </summary>
public sealed record ConversationStats
{
    public int TotalSessions { get; init; }
    public int TotalTurns { get; init; }
    public int CurrentSessionTurns { get; init; }
    public DateTime? OldestMemory { get; init; }
    public DateTime? CurrentSessionStart { get; init; }
}