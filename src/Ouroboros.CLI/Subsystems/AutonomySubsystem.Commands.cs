// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using System.Text;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.SelfModel;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Subsystems.Autonomy;
using Spectre.Console;
using static Ouroboros.Application.Tools.AutonomousTools;
using MetaAgentStatus = Ouroboros.Agent.MetaAI.AgentStatus;

/// <summary>
/// Partial: Command handlers for self-execution, sub-agents, epics, goals, delegation,
/// self-model inspection, and self-evaluation.
/// </summary>
public sealed partial class AutonomySubsystem
{
    // ═══════════════════════════════════════════════════════════════════════════
    // SELF-EXECUTION COMMAND HANDLERS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Handles self-execution commands.
    /// </summary>
    internal async Task<string> SelfExecCommandAsync(string subCommand)
    {
        var cmd = subCommand.ToLowerInvariant().Trim();

        if (cmd is "" or "status")
        {
            var status = SelfExecutionEnabled ? "Active" : "Disabled";
            var queueCount = GoalQueue.Count;
            return $@"Self-Execution Status:
• Status: {status}
• Queued Goals: {queueCount}
• Completed: (tracked in global workspace)

Commands:
  selfexec start    - Enable autonomous execution
  selfexec stop     - Disable autonomous execution
  selfexec queue    - Show queued goals";
        }

        if (cmd == "start")
        {
            if (!SelfExecutionEnabled)
            {
                SelfExecutionCts?.Dispose();
                SelfExecutionCts = new CancellationTokenSource();
                SelfExecutionEnabled = true;
                SelfExecutionTask = Task.Run(SelfExecutionLoopAsync, SelfExecutionCts.Token);
            }
            return "Self-execution enabled. I will autonomously pursue queued goals.";
        }

        if (cmd == "stop")
        {
            SelfExecutionEnabled = false;
            SelfExecutionCts?.Cancel();
            return "Self-execution disabled. Goals will no longer be automatically executed.";
        }

        if (cmd == "queue")
        {
            if (GoalQueue.IsEmpty)
            {
                return "Goal queue is empty. Use 'goal add <description>' to add goals.";
            }
            var goals = GoalQueue.ToArray();
            var sb = new StringBuilder("Queued Goals:\n");
            for (int i = 0; i < goals.Length; i++)
            {
                sb.AppendLine($"  {i + 1}. [{goals[i].Priority}] {goals[i].Description}");
            }
            return sb.ToString();
        }

        return $"Unknown self-exec command: {subCommand}. Try 'selfexec status'.";
    }

    /// <summary>
    /// Handles sub-agent commands.
    /// </summary>
    internal async Task<string> SubAgentCommandAsync(string subCommand)
    {
        var cmd = subCommand.ToLowerInvariant().Trim();

        if (cmd is "" or "status" or "list")
        {
            if (DistributedOrchestrator == null)
            {
                return "Sub-agent orchestration not initialized.";
            }

            var agents = DistributedOrchestrator.GetAgentStatus();
            var sb = new StringBuilder("Registered Sub-Agents:\n");
            foreach (var agent in agents)
            {
                var statusIcon = agent.Status switch
                {
                    MetaAgentStatus.Available => "✓",
                    MetaAgentStatus.Busy => "⏳",
                    MetaAgentStatus.Offline => "✗",
                    _ => "?"
                };
                sb.AppendLine($"  {statusIcon} {agent.Name} ({agent.AgentId})");
                sb.AppendLine($"      Capabilities: {string.Join(", ", agent.Capabilities.Take(5))}");
                sb.AppendLine($"      Last heartbeat: {agent.LastHeartbeat:HH:mm:ss}");
            }
            return sb.ToString();
        }

        if (cmd.StartsWith("spawn "))
        {
            var agentName = cmd[6..].Trim();
            return await SpawnSubAgentAsync(agentName);
        }

        if (cmd.StartsWith("remove "))
        {
            var agentId = cmd[7..].Trim();
            _subAgentManager.RemoveSubAgent(agentId);
            return $"Removed sub-agent: {agentId}";
        }

        await Task.CompletedTask;
        return $"Unknown subagent command. Try: subagent list, subagent spawn <name>, subagent remove <id>";
    }

    /// <summary>
    /// Spawns a new sub-agent with specialized capabilities.
    /// Delegates to <see cref="SubAgentOrchestrationManager"/>.
    /// </summary>
    internal Task<string> SpawnSubAgentAsync(string agentName)
    {
        return _subAgentManager.SpawnSubAgentAsync(agentName, Models.ChatModel);
    }

    /// <summary>
    /// Handles epic orchestration commands.
    /// </summary>
    internal async Task<string> EpicCommandAsync(string subCommand)
    {
        var cmd = subCommand.ToLowerInvariant().Trim();

        if (cmd is "" or "status" or "list")
        {
            return "Epic Orchestration:\n• Use 'epic create <title>' to create a new epic\n• Use 'epic add <epic#> <sub-issue>' to add sub-issues";
        }

        if (cmd.StartsWith("create "))
        {
            var title = cmd[7..].Trim();
            if (EpicOrchestrator != null)
            {
                var epicNumber = new Random().Next(1000, 9999);
                var result = await EpicOrchestrator.RegisterEpicAsync(
                    epicNumber, title, "", new List<int>());

                if (result.IsSuccess)
                {
                    return $"Created epic #{epicNumber}: {title}";
                }
                return $"Failed to create epic: {result.Error}";
            }
            return "Epic orchestrator not initialized.";
        }

        await Task.CompletedTask;
        return $"Unknown epic command: {subCommand}";
    }

    /// <summary>
    /// Handles goal queue commands.
    /// </summary>
    internal async Task<string> GoalCommandAsync(string subCommand)
    {
        var cmd = subCommand.ToLowerInvariant().Trim();

        if (cmd is "" or "list")
        {
            if (GoalQueue.IsEmpty)
            {
                return "No goals in queue. Use 'goal add <description>' to add a goal.";
            }
            var goals = GoalQueue.ToArray();
            var sb = new StringBuilder("Goal Queue:\n");
            for (int i = 0; i < goals.Length; i++)
            {
                sb.AppendLine($"  {i + 1}. [{goals[i].Priority}] {goals[i].Description}");
            }
            return sb.ToString();
        }

        if (cmd.StartsWith("add "))
        {
            var description = subCommand[4..].Trim();
            var priority = description.Contains("urgent") ? GoalPriority.High
                : description.Contains("later") ? GoalPriority.Low
                : GoalPriority.Normal;

            var goal = new AutonomousGoal(Guid.NewGuid(), description, priority, DateTime.UtcNow);
            GoalQueue.Enqueue(goal);

            return $"Added goal to queue: {description} (Priority: {priority})";
        }

        if (cmd == "clear")
        {
            while (GoalQueue.TryDequeue(out _)) { }
            return "Goal queue cleared.";
        }

        await Task.CompletedTask;
        return "Goal commands: goal list, goal add <description>, goal clear";
    }

    /// <summary>
    /// Handles task delegation to sub-agents.
    /// </summary>
    internal async Task<string> DelegateCommandAsync(string taskDescription)
    {
        if (string.IsNullOrWhiteSpace(taskDescription))
        {
            return "Usage: delegate <task description>";
        }

        if (DistributedOrchestrator == null || Orchestrator == null)
        {
            return "Delegation requires sub-agent orchestration to be initialized.";
        }

        // Create a plan for the task
        var planResult = await Orchestrator.PlanAsync(taskDescription);
        if (!planResult.IsSuccess)
        {
            return $"Could not create plan for delegation: {planResult.Error}";
        }

        // Execute distributed
        var execResult = await DistributedOrchestrator.ExecuteDistributedAsync(planResult.Value);
        if (execResult.IsSuccess)
        {
            var agents = execResult.Value.Metadata.GetValueOrDefault("agents_used", 0);
            return $"Task delegated and completed using {agents} agent(s):\n{execResult.Value.FinalOutput}";
        }

        return $"Delegation failed: {execResult.Error}";
    }

    /// <summary>
    /// Handles self-model inspection commands.
    /// </summary>
    internal async Task<string> SelfModelCommandAsync(string subCommand)
    {
        var cmd = subCommand.ToLowerInvariant().Trim();

        if (cmd is "" or "status" or "identity")
        {
            if (IdentityGraph == null)
            {
                return "Self-model not initialized.";
            }

            var state = await IdentityGraph.GetStateAsync();
            var sb = new StringBuilder();
            sb.AppendLine("╔═══════════════════════════════════════╗");
            sb.AppendLine("║         SELF-MODEL IDENTITY           ║");
            sb.AppendLine("╠═══════════════════════════════════════╣");
            sb.AppendLine($"║ Agent ID: {state.AgentId.ToString()[..8],-27} ║");
            sb.AppendLine($"║ Name: {state.Name,-31} ║");
            sb.AppendLine("╠═══════════════════════════════════════╣");
            sb.AppendLine("║ Capabilities:                         ║");

            if (CapabilityRegistry != null)
            {
                var caps = await CapabilityRegistry.GetCapabilitiesAsync();
                foreach (var cap in caps.Take(5))
                {
                    sb.AppendLine($"║   • {cap.Name,-20} ({cap.SuccessRate:P0}) ║");
                }
            }

            sb.AppendLine("╚═══════════════════════════════════════╝");
            return sb.ToString();
        }

        if (cmd == "capabilities" || cmd == "caps")
        {
            if (CapabilityRegistry == null)
            {
                return "Capability registry not initialized.";
            }

            var caps = await CapabilityRegistry.GetCapabilitiesAsync();
            var sb = new StringBuilder("Agent Capabilities:\n");
            foreach (var cap in caps)
            {
                sb.AppendLine($"  • {cap.Name}");
                sb.AppendLine($"      Description: {cap.Description}");
                sb.AppendLine($"      Success Rate: {cap.SuccessRate:P0} ({cap.UsageCount} uses)");
                var toolsList = cap.RequiredTools?.Any() == true ? string.Join(", ", cap.RequiredTools) : "none";
                sb.AppendLine($"      Required Tools: {toolsList}");
            }
            return sb.ToString();
        }

        if (cmd == "workspace")
        {
            if (GlobalWorkspace == null)
            {
                return "Global workspace not initialized.";
            }

            var items = GlobalWorkspace.GetItems();
            if (!items.Any())
            {
                return "Global workspace is empty.";
            }

            var sb = new StringBuilder("Global Workspace Contents:\n");
            foreach (var item in items.Take(10))
            {
                sb.AppendLine($"  [{item.Priority}] {item.Content[..Math.Min(50, item.Content.Length)]}...");
                sb.AppendLine($"      Source: {item.Source} | Created: {item.CreatedAt:HH:mm:ss}");
            }
            return sb.ToString();
        }

        return "Self-model commands: selfmodel status, selfmodel capabilities, selfmodel workspace";
    }

    /// <summary>
    /// Handles self-evaluation commands.
    /// </summary>
    internal async Task<string> EvaluateCommandAsync(string subCommand)
    {
        var cmd = subCommand.ToLowerInvariant().Trim();

        if (SelfEvaluator == null)
        {
            return "Self-evaluator not initialized. Requires orchestrator and skill registry.";
        }

        if (cmd is "" or "performance" or "assess")
        {
            var result = await SelfEvaluator.EvaluatePerformanceAsync();
            if (result.IsSuccess)
            {
                var assessment = result.Value;
                var sb = new StringBuilder();
                sb.AppendLine("╔═══════════════════════════════════════╗");
                sb.AppendLine("║       SELF-ASSESSMENT REPORT          ║");
                sb.AppendLine("╠═══════════════════════════════════════╣");
                sb.AppendLine($"║ Overall Performance: {assessment.OverallPerformance:P0,-15} ║");
                sb.AppendLine($"║ Confidence Calibration: {assessment.ConfidenceCalibration:P0,-12} ║");
                sb.AppendLine($"║ Skill Acquisition Rate: {assessment.SkillAcquisitionRate:F2,-12} ║");
                sb.AppendLine("╠═══════════════════════════════════════╣");

                if (assessment.Strengths.Any())
                {
                    sb.AppendLine("║ Strengths:                            ║");
                    foreach (var s in assessment.Strengths.Take(3))
                    {
                        sb.AppendLine($"║   ✓ {s,-33} ║");
                    }
                }

                if (assessment.Weaknesses.Any())
                {
                    sb.AppendLine("║ Areas for Improvement:                ║");
                    foreach (var w in assessment.Weaknesses.Take(3))
                    {
                        sb.AppendLine($"║   △ {w,-33} ║");
                    }
                }

                sb.AppendLine("╚═══════════════════════════════════════╝");
                sb.AppendLine();
                sb.AppendLine("Summary:");
                sb.AppendLine(assessment.Summary);

                return sb.ToString();
            }
            return $"Evaluation failed: {result.Error}";
        }

        return "Evaluate commands: evaluate performance";
    }
}
