using System.ComponentModel.DataAnnotations;

namespace Ouroboros.Application.Integration;

/// <summary>Episodic memory configuration.</summary>
public sealed class EpisodicMemoryConfig
{
    /// <summary>Gets or sets vector store connection string.</summary>
    [Required]
    [Url]
    public string VectorStoreConnectionString { get; set; } = "http://localhost:6333";

    /// <summary>Gets or sets maximum memory size.</summary>
    [Range(100, 1000000)]
    public int MaxMemorySize { get; set; } = 10000;

    /// <summary>Gets or sets consolidation interval in hours.</summary>
    [Range(1, 168)]
    public int ConsolidationIntervalHours { get; set; } = 24;

    /// <summary>Gets or sets whether auto-consolidation is enabled.</summary>
    public bool EnableAutoConsolidation { get; set; } = true;
}