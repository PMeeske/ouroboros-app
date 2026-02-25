namespace Ouroboros.Application.Integration;

/// <summary>
/// Event fired when learning occurs.
/// </summary>
public sealed record LearningCompletedEvent(
    Guid EventId,
    DateTime Timestamp,
    string Source,
    int EpisodesProcessed,
    int RulesLearned) : SystemEvent(EventId, Timestamp, Source);