// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using Ouroboros.Application.Swarm;

/// <summary>
/// Manages claude-flow swarm orchestration: MCP server lifecycle, agent spawning,
/// task orchestration, and ethics-gated self-modification through swarm operations.
/// </summary>
public interface ISwarmSubsystem : IAgentSubsystem
{
    /// <summary>Whether the claude-flow MCP server is connected and responsive.</summary>
    bool IsSwarmConnected { get; }

    /// <summary>Initializes the swarm with the configured topology.</summary>
    Task<SwarmInitResult> InitSwarmAsync(CancellationToken ct = default);

    /// <summary>Spawns a new agent in the swarm.</summary>
    Task<AgentSpawnResult> SpawnAgentAsync(
        string type, string? name = null,
        IReadOnlyList<string>? capabilities = null,
        CancellationToken ct = default);

    /// <summary>Orchestrates a task across swarm agents.</summary>
    Task<TaskOrchestrationResult> OrchestrateAsync(
        string task,
        string strategy = "adaptive",
        int maxAgents = 5,
        string priority = "medium",
        CancellationToken ct = default);

    /// <summary>Gets current swarm status.</summary>
    Task<SwarmStatusResult> GetStatusAsync(CancellationToken ct = default);

    /// <summary>Lists all agents in the swarm.</summary>
    Task<IReadOnlyList<AgentListEntry>> ListAgentsAsync(
        string filter = "all", CancellationToken ct = default);

    /// <summary>Shuts down the swarm gracefully.</summary>
    Task ShutdownSwarmAsync(CancellationToken ct = default);

    /// <summary>Gets swarm health status.</summary>
    Task<SwarmHealthResult> GetSwarmHealthAsync(CancellationToken ct = default);
}
