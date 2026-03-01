// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Commands.Swarm;

using Ouroboros.CLI.Subsystems;

/// <summary>
/// Handles interactive swarm commands routed from CommandRoutingSubsystem.
/// Subcommands: init, status, spawn, orchestrate, agents, shutdown, health, help.
/// </summary>
public static class SwarmCommandHandler
{
    public static async Task<string> HandleAsync(
        string argument,
        SwarmSubsystem swarmSub,
        CancellationToken ct = default)
    {
        var parts = argument.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var subcommand = parts.Length > 0 ? parts[0].ToLowerInvariant() : "status";
        var rest = parts.Length > 1 ? parts[1] : "";

        try
        {
            return subcommand switch
            {
                "init" => await HandleInitAsync(swarmSub, ct),
                "status" => await HandleStatusAsync(swarmSub, ct),
                "spawn" => await HandleSpawnAsync(rest, swarmSub, ct),
                "orchestrate" or "orch" => await HandleOrchestrateAsync(rest, swarmSub, ct),
                "agents" or "list" => await HandleListAgentsAsync(rest, swarmSub, ct),
                "shutdown" or "stop" => await HandleShutdownAsync(swarmSub, ct),
                "health" => await HandleHealthAsync(swarmSub, ct),
                "help" or "?" => GetSwarmHelp(),
                _ => $"Unknown swarm subcommand: {subcommand}. Use 'swarm help' for available commands.",
            };
        }
        catch (InvalidOperationException ex)
        {
            return $"Swarm error: {ex.Message}";
        }
    }

    private static async Task<string> HandleInitAsync(SwarmSubsystem sub, CancellationToken ct)
    {
        var result = await sub.InitSwarmAsync(ct);
        return result.Success
            ? $"Swarm initialized: {result.SwarmId} (topology: {result.Topology}, max agents: {result.MaxAgents})"
            : $"Swarm init failed: {result.Message}";
    }

    private static async Task<string> HandleStatusAsync(SwarmSubsystem sub, CancellationToken ct)
    {
        if (!sub.IsSwarmConnected)
            return "Swarm not connected. Use 'swarm init' to start.";

        var status = await sub.GetStatusAsync(ct);
        return $"Swarm: {(status.Active ? "ACTIVE" : "INACTIVE")}\n" +
               $"  ID: {status.SwarmId}\n" +
               $"  Topology: {status.Topology}\n" +
               $"  Agents: {status.AgentCount}";
    }

    private static async Task<string> HandleSpawnAsync(
        string args, SwarmSubsystem sub, CancellationToken ct)
    {
        var argParts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (argParts.Length == 0)
            return "Usage: swarm spawn <type> [--name <name>]\n" +
                   "Types: coder, reviewer, tester, researcher, analyst, optimizer, coordinator";

        var type = argParts[0];
        string? name = null;
        for (int i = 1; i < argParts.Length - 1; i++)
        {
            if (argParts[i] == "--name") name = argParts[i + 1];
        }

        var result = await sub.SpawnAgentAsync(type, name, ct: ct);
        return result.Success
            ? $"Agent spawned: {result.AgentId} (type: {result.AgentType}, name: {result.Name ?? "auto"})"
            : $"Spawn failed: {result.Message}";
    }

    private static async Task<string> HandleOrchestrateAsync(
        string task, SwarmSubsystem sub, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(task))
            return "Usage: swarm orchestrate <task description>";

        var result = await sub.OrchestrateAsync(task, ct: ct);
        return result.Success
            ? $"Task orchestrated: {result.TaskId}\n  Status: {result.Status}\n  Result: {result.Result}"
            : $"Orchestration failed: {result.Result}";
    }

    private static async Task<string> HandleListAgentsAsync(
        string filter, SwarmSubsystem sub, CancellationToken ct)
    {
        var agents = await sub.ListAgentsAsync(
            string.IsNullOrWhiteSpace(filter) ? "all" : filter, ct);

        if (agents.Count == 0) return "No agents in swarm.";

        var lines = agents.Select(a =>
            $"  [{a.Status}] {a.AgentId} ({a.Type}) {a.Name ?? ""}");
        return $"Swarm agents ({agents.Count}):\n{string.Join("\n", lines)}";
    }

    private static async Task<string> HandleShutdownAsync(SwarmSubsystem sub, CancellationToken ct)
    {
        await sub.ShutdownSwarmAsync(ct);
        return "Swarm shutdown complete.";
    }

    private static async Task<string> HandleHealthAsync(SwarmSubsystem sub, CancellationToken ct)
    {
        var health = await sub.GetSwarmHealthAsync(ct);
        return health.Healthy
            ? $"Swarm health: OK\n  {health.Details}"
            : $"Swarm health: DEGRADED\n  {health.Status}: {health.Details}";
    }

    private static string GetSwarmHelp() =>
        """
        Swarm Commands:
          swarm init                    - Initialize claude-flow swarm
          swarm status                  - Show swarm status
          swarm spawn <type> [--name N] - Spawn an agent (coder, reviewer, tester, etc.)
          swarm orchestrate <task>      - Orchestrate a task across agents
          swarm agents [filter]         - List swarm agents (all|active|idle|busy)
          swarm shutdown                - Shut down the swarm
          swarm health                  - Swarm health check
          swarm help                    - This help message
        """;
}
