namespace Ouroboros.Application.Tools;

/// <summary>
/// Learning statistics.
/// </summary>
public sealed record LearningStats(
    int TotalToolExecutions,
    int TotalSkillExecutions,
    int TotalPipelineExecutions,
    int SuccessfulExecutions,
    int LearnedPatterns,
    int ConceptGraphNodes,
    int ExecutionLogSize);