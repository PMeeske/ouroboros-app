namespace Ouroboros.Application.Services;

/// <summary>
/// Result of verifying a claim about the codebase.
/// </summary>
public sealed record ClaimVerification
{
    /// <summary>Whether the claim is valid/true.</summary>
    public bool IsValid { get; init; }

    /// <summary>Reason for the verification result.</summary>
    public string Reason { get; init; } = "";

    /// <summary>Type of claim being verified.</summary>
    public string ClaimType { get; init; } = "";
}