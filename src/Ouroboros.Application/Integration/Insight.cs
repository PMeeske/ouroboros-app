using Ouroboros.Pipeline.Memory;

namespace Ouroboros.Application.Integration;

/// <summary>
/// Represents an insight learned from experience.
/// </summary>
public sealed record Insight(
    string Description,
    double Confidence,
    List<Episode> SupportingEpisodes);