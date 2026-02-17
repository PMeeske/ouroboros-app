namespace Ouroboros.Application.SelfAssembly;

/// <summary>
/// Safety constraint for blueprint validation.
/// </summary>
public sealed record SafetyConstraint
{
    /// <summary>Name of the constraint.</summary>
    public required string Name { get; init; }

    /// <summary>Description of what it checks.</summary>
    public required string Description { get; init; }

    /// <summary>MeTTa expression that must evaluate to True.</summary>
    public required string MeTTaExpression { get; init; }

    /// <summary>Weight in the final safety score (0-1).</summary>
    public double Weight { get; init; } = 1.0;

    /// <summary>Whether violation is critical (blocks assembly).</summary>
    public bool IsCritical { get; init; } = false;
}