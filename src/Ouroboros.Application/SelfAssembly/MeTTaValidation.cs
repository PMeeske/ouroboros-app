namespace Ouroboros.Application.SelfAssembly;

/// <summary>
/// Result of MeTTa-based blueprint validation.
/// </summary>
public record MeTTaValidation(
    bool IsValid,
    double SafetyScore,
    IReadOnlyList<string> Violations,
    IReadOnlyList<string> Warnings,
    string MeTTaExpression);