namespace Ouroboros.Application.SelfAssembly;

/// <summary>
/// Represents a capability gap identified by the system.
/// </summary>
public sealed record CapabilityGap
{
    /// <summary>Description of the missing capability.</summary>
    public required string Description { get; init; }

    /// <summary>Why this capability is needed.</summary>
    public required string Rationale { get; init; }

    /// <summary>Estimated importance (0-1).</summary>
    public double Importance { get; init; }

    /// <summary>Topics that would benefit from this capability.</summary>
    public required IReadOnlyList<string> AffectedTopics { get; init; }

    /// <summary>Suggested capabilities to implement.</summary>
    public required IReadOnlyList<NeuronCapability> SuggestedCapabilities { get; init; }

    /// <summary>The source that identified this gap.</summary>
    public required string IdentifiedBy { get; init; }

    /// <summary>When this gap was identified.</summary>
    public DateTime IdentifiedAt { get; init; } = DateTime.UtcNow;
}