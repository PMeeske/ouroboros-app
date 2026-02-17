using System.ComponentModel.DataAnnotations;

namespace Ouroboros.Application.Integration;

/// <summary>Multi-agent configuration.</summary>
public sealed class MultiAgentConfig
{
    /// <summary>Gets or sets maximum agents.</summary>
    [Range(1, 100)]
    public int MaxAgents { get; set; } = 10;

    /// <summary>Gets or sets consensus protocol.</summary>
    [Required]
    public string ConsensusProtocol { get; set; } = "raft";

    /// <summary>Gets or sets whether knowledge sharing is enabled.</summary>
    public bool EnableKnowledgeSharing { get; set; } = true;
}