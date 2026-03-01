// <copyright file="SwarmModels.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Swarm;

/// <summary>
/// Result of initializing a claude-flow swarm.
/// </summary>
public sealed record SwarmInitResult(
    bool Success,
    string SwarmId,
    string Topology,
    int MaxAgents,
    string Message);

/// <summary>
/// Current status of a running swarm.
/// </summary>
public sealed record SwarmStatusResult(
    bool Active,
    string SwarmId,
    int AgentCount,
    string Topology,
    string RawJson);

/// <summary>
/// Health check result for the swarm.
/// </summary>
public sealed record SwarmHealthResult(
    bool Healthy,
    string Status,
    string Details);

/// <summary>
/// Result of spawning a new agent in the swarm.
/// </summary>
public sealed record AgentSpawnResult(
    bool Success,
    string AgentId,
    string AgentType,
    string? Name,
    string Message);

/// <summary>
/// Status of a specific agent.
/// </summary>
public sealed record AgentStatusResult(
    string AgentId,
    string Status,
    string Type,
    string Details);

/// <summary>
/// Entry in the agent list.
/// </summary>
public sealed record AgentListEntry(
    string AgentId,
    string Type,
    string Status,
    string? Name);

/// <summary>
/// Result of orchestrating a task across the swarm.
/// </summary>
public sealed record TaskOrchestrationResult(
    bool Success,
    string TaskId,
    string Status,
    string Result);

/// <summary>
/// A single result from a memory search.
/// </summary>
public sealed record MemorySearchResult(
    string Key,
    string Value,
    double Score);

/// <summary>
/// Configuration for connecting to the claude-flow MCP server.
/// </summary>
public sealed record ClaudeFlowConfig
{
    /// <summary>The executable command (e.g. "npx" or "claude-flow").</summary>
    public string Command { get; init; } = "npx";

    /// <summary>Arguments for the command.</summary>
    public string[] Args { get; init; } = ["-y", "@claude-flow/cli@latest", "mcp", "start"];

    /// <summary>Swarm topology (hierarchical-mesh, mesh, ring, star).</summary>
    public string Topology { get; init; } = "hierarchical-mesh";

    /// <summary>Maximum number of agents.</summary>
    public int MaxAgents { get; init; } = 15;

    /// <summary>Distribution strategy (specialized, balanced, adaptive).</summary>
    public string Strategy { get; init; } = "specialized";

    /// <summary>Memory backend (hybrid, memory, disk).</summary>
    public string MemoryBackend { get; init; } = "hybrid";
}
