namespace Ouroboros.CLI.Commands;

/// <summary>
/// Represents an autonomous goal for self-execution.
/// </summary>
public sealed record AutonomousGoal(
    Guid Id,
    string Description,
    GoalPriority Priority,
    DateTime CreatedAt);