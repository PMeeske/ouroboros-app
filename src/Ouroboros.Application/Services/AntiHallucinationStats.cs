namespace Ouroboros.Application.Services;

/// <summary>
/// Statistics about anti-hallucination measures.
/// </summary>
public sealed record AntiHallucinationStats
{
    /// <summary>Number of detected hallucinations (blocked false claims).</summary>
    public int HallucinationCount { get; init; }

    /// <summary>Number of verified successful actions.</summary>
    public int VerifiedActionCount { get; init; }

    /// <summary>Number of actions pending verification.</summary>
    public int PendingVerifications { get; init; }

    /// <summary>Recent verification results.</summary>
    public List<ModificationVerification> RecentVerifications { get; init; } = [];

    /// <summary>Ratio of hallucinations to total actions (0-1).</summary>
    public double HallucinationRate { get; init; }
}