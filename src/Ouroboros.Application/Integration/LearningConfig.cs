using Ouroboros.Pipeline.Memory;

namespace Ouroboros.Application.Integration;

/// <summary>
/// Configuration for learning from experience.
/// </summary>
public sealed record LearningConfig(
    bool ConsolidateMemories = true,
    bool UpdateAdapters = true,
    bool ExtractRules = true,
    ConsolidationStrategy ConsolidationStrategy = ConsolidationStrategy.Abstract)
{
    /// <summary>Gets the default learning configuration.</summary>
    public static LearningConfig Default => new();
}