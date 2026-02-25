namespace Ouroboros.Application.SelfAssembly;

/// <summary>
/// Result of a single constraint check.
/// </summary>
public sealed record ConstraintResult(
    SafetyConstraint Constraint,
    bool Passed,
    string? FailureReason);