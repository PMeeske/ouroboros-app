namespace Ouroboros.Application.Integration;

/// <summary>
/// Result of learning from experience.
/// </summary>
public sealed record LearningResult(
    int EpisodesProcessed,
    int RulesLearned,
    int AdaptersUpdated,
    double PerformanceImprovement,
    List<Insight> Insights);