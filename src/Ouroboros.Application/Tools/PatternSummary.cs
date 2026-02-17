namespace Ouroboros.Application.Tools;

/// <summary>
/// Summary of a learned pattern.
/// </summary>
public sealed record PatternSummary(
    string PatternType,
    string GoalDescription,
    List<string> Actions,
    double SuccessRate);