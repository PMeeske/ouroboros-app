namespace Ouroboros.Application.Tools;

/// <summary>
/// Represents a learned interconnection pattern between tools, skills, and goals.
/// </summary>
public sealed record InterconnectedPattern(
    string Id,
    string PatternType,
    string GoalDescription,
    List<string> ToolSequence,
    List<string> SkillSequence,
    double SuccessRate,
    int UsageCount,
    DateTime CreatedAt,
    DateTime LastUsed,
    float[] EmbeddingVector);