// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.
namespace Ouroboros.CLI.Subsystems.Autonomy;

using System.Collections.Concurrent;
using Ouroboros.CLI.Commands;
using Spectre.Console;
using MetaAgentStatus = Ouroboros.Agent.MetaAI.AgentStatus;

/// <summary>
/// Manages sub-agent spawning, monitoring, cleanup, and epic orchestration.
/// Extracted from <see cref="AutonomySubsystem"/> to reduce class size.
/// </summary>
internal sealed class SubAgentOrchestrationManager
{
    // ── State ────────────────────────────────────────────────────────────
    public ConcurrentDictionary<string, SubAgentInstance> SubAgents { get; } = new();
    public IDistributedOrchestrator? DistributedOrchestrator { get; private set; }
    public IEpicBranchOrchestrator? EpicOrchestrator { get; private set; }

    /// <summary>
    /// Initializes the sub-agent orchestration subsystem.
    /// </summary>
    public async Task InitializeCoreAsync(SubsystemInitContext ctx)
    {
        try
        {
            var safety = new SafetyGuard();
            DistributedOrchestrator = new DistributedOrchestrator(safety);

            var selfCaps = new HashSet<string>
            {
                "planning", "reasoning", "coding", "research", "analysis",
                "summarization", "tool_use", "metta_reasoning"
            };
            var selfAgent = new AgentInfo(
                "ouroboros-primary", ctx.Config.Persona, selfCaps,
                MetaAgentStatus.Available, DateTime.UtcNow);
            DistributedOrchestrator.RegisterAgent(selfAgent);

            EpicOrchestrator = new EpicBranchOrchestrator(
                DistributedOrchestrator,
                new EpicBranchConfig(
                    BranchPrefix: "ouroboros-epic",
                    AgentPoolPrefix: "sub-agent",
                    AutoCreateBranches: true,
                    AutoAssignAgents: true,
                    MaxConcurrentSubIssues: 5));

            ctx.Output.RecordInit("Sub-Agents", true, "distributed orchestration (1 agent)");
            await Task.CompletedTask;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn($"SubAgent orchestration failed: {ex.Message}")}");
        }
    }

    /// <summary>
    /// Spawns a new sub-agent with capabilities inferred from its name.
    /// </summary>
    public async Task<string> SpawnSubAgentAsync(
        string agentName,
        Ouroboros.Abstractions.Core.IChatCompletionModel? chatModel)
    {
        if (DistributedOrchestrator == null)
            return "Sub-agent orchestration not initialized.";

        var agentId = $"sub-{agentName.ToLowerInvariant()}-{Guid.NewGuid().ToString()[..8]}";
        var capabilities = InferCapabilities(agentName);

        var agent = new AgentInfo(
            agentId, agentName, capabilities,
            MetaAgentStatus.Available, DateTime.UtcNow);

        DistributedOrchestrator.RegisterAgent(agent);

        var subAgent = new SubAgentInstance(agentId, agentName, capabilities, chatModel);
        SubAgents[agentId] = subAgent;

        await Task.CompletedTask;
        return $"Spawned sub-agent '{agentName}' ({agentId}) with capabilities: {string.Join(", ", capabilities)}";
    }

    /// <summary>
    /// Removes a previously registered sub-agent.
    /// </summary>
    public void RemoveSubAgent(string agentId)
    {
        DistributedOrchestrator?.UnregisterAgent(agentId);
        SubAgents.TryRemove(agentId, out _);
    }

    /// <summary>
    /// Clears all sub-agents during disposal.
    /// </summary>
    public void Clear() => SubAgents.Clear();

    // ──────────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────────

    private static HashSet<string> InferCapabilities(string agentName)
    {
        var capabilities = new HashSet<string>();
        var lowerName = agentName.ToLowerInvariant();

        if (lowerName.Contains("code") || lowerName.Contains("dev"))
            capabilities.UnionWith(["coding", "debugging", "refactoring", "testing"]);
        else if (lowerName.Contains("research") || lowerName.Contains("analyst"))
            capabilities.UnionWith(["research", "analysis", "summarization", "web_search"]);
        else if (lowerName.Contains("plan") || lowerName.Contains("architect"))
            capabilities.UnionWith(["planning", "architecture", "design", "decomposition"]);
        else
            capabilities.UnionWith(["general", "chat", "reasoning"]);

        return capabilities;
    }
}
