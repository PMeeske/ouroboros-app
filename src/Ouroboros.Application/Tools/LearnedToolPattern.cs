namespace Ouroboros.Application.Tools;

/// <summary>
/// Represents a learned tool pattern stored in Qdrant.
/// </summary>
public sealed record LearnedToolPattern(
    string Id,
    string Goal,
    string ToolName,
    ToolConfiguration Configuration,
    double SuccessRate,
    int UsageCount,
    DateTime CreatedAt,
    DateTime LastUsed,
    List<string> RelatedGoals);