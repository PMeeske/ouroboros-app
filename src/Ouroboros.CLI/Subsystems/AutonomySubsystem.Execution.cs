// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using System.Text;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.SelfModel;
using Ouroboros.Application.Tools;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Network;
using LangChain.DocumentLoaders;
using Spectre.Console;
using static Ouroboros.Application.Tools.AutonomousTools;
using PipelineReasoningStep = Ouroboros.Domain.Events.ReasoningStep;

/// <summary>
/// Partial: Self-execution loop, goal execution, DSL pipeline execution, and goal tracking.
/// </summary>
public sealed partial class AutonomySubsystem
{
    /// <summary>
    /// Background loop for self-execution of queued goals.
    /// </summary>
    internal async Task SelfExecutionLoopAsync()
    {
        while (SelfExecutionEnabled && !SelfExecutionCts?.Token.IsCancellationRequested == true)
        {
            try
            {
                if (GoalQueue.TryDequeue(out var goal))
                {
                    AnsiConsole.MarkupLine($"\n  [rgb(148,103,189)]{Markup.Escape($"[self-exec] Starting autonomous goal: {goal.Description}")}[/]");

                    var startTime = DateTime.UtcNow;
                    string result;
                    bool success = true;

                    try
                    {
                        // Check if this is a DSL goal (starts with pipe syntax)
                        if (goal.Description.Contains("|") || goal.Description.StartsWith("pipeline:"))
                        {
                            result = await ExecuteDslGoalAsync(goal);
                        }
                        else
                        {
                            result = await ExecuteGoalAutonomouslyAsync(goal);
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        success = false;
                        result = $"Execution failed: {ex.Message}";
                    }

                    var duration = DateTime.UtcNow - startTime;

                    // Track capability usage for self-improvement
                    await TrackGoalExecutionAsync(goal, success, duration);

                    // Reify execution into network state
                    ReifyGoalExecution(goal, result, success, duration);

                    // Update global workspace with result
                    var priority = goal.Priority switch
                    {
                        GoalPriority.Critical => WorkspacePriority.Critical,
                        GoalPriority.High => WorkspacePriority.High,
                        GoalPriority.Normal => WorkspacePriority.Normal,
                        _ => WorkspacePriority.Low
                    };
                    GlobalWorkspace?.AddItem(
                        $"Goal completed: {goal.Description}\nResult: {result}\nDuration: {duration.TotalSeconds:F2}s",
                        priority,
                        "self-execution",
                        new List<string> { "goal", success ? "completed" : "failed" });

                    // Trigger autonomous reflection on completion
                    if (success)
                    {
                        // Learn from successful execution
                        await ExecuteAutonomousActionAsync("Learn", $"Successful goal execution: {goal.Description}");
                    }
                    else
                    {
                        // Reflect on failure to improve
                        await ExecuteAutonomousActionAsync("Reflect", $"Failed goal: {goal.Description}. Result: {result}");
                    }

                    // Trigger self-evaluation periodically
                    if (GoalQueue.IsEmpty && SelfEvaluator != null)
                    {
                        await PerformPeriodicSelfEvaluationAsync();
                    }

                    if (success)
                        AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[self-exec] Goal completed: {goal.Description} ({duration.TotalSeconds:F2}s)")}");
                    else
                        AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn($"[self-exec] Goal failed: {goal.Description} ({duration.TotalSeconds:F2}s)")}");
                }
                else
                {
                    // Idle time - check for self-improvement opportunities and generate autonomous thoughts
                    await CheckSelfImprovementOpportunitiesAsync();

                    // Periodically run autonomous introspection cycles
                    if (Random.Shared.NextDouble() < 0.05) // 5% chance per idle cycle
                    {
                        await ExecuteAutonomousActionAsync("SelfImprove", "idle_introspection");
                    }

                    await Task.Delay(1000, SelfExecutionCts?.Token ?? CancellationToken.None);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn($"[self-exec] Error: {ex.Message}")}");
            }
        }
    }

    /// <summary>
    /// Executes a DSL pipeline goal with full reification.
    /// </summary>
    internal async Task<string> ExecuteDslGoalAsync(AutonomousGoal goal)
    {
        var dsl = goal.Description.StartsWith("pipeline:")
            ? goal.Description[9..].Trim()
            : goal.Description;

        if (Models.Embedding == null || Models.Llm == null)
        {
            return "DSL execution requires LLM and embeddings to be initialized.";
        }

        var store = new TrackedVectorStore();
        var dataSource = DataSource.FromPath(".");
        var branch = new PipelineBranch($"goal-{goal.Id.ToString()[..8]}", store, dataSource);

        var state = new CliPipelineState
        {
            Branch = branch,
            Llm = Models.Llm,
            Tools = Tools.Tools,
            Embed = Models.Embedding,
            Trace = Config.Debug,
            NetworkTracker = NetworkTracker
        };

        // Track the branch for reification
        NetworkTracker?.TrackBranch(branch);

        var step = PipelineDsl.Build(dsl);
        state = await step(state);

        // Final reification update
        NetworkTracker?.UpdateBranch(state.Branch);

        // Extract output
        var lastReasoning = state.Branch.Events.OfType<PipelineReasoningStep>().LastOrDefault();
        return lastReasoning?.State.Text ?? state.Output ?? "Pipeline completed without output.";
    }

    /// <summary>
    /// Tracks goal execution for capability self-improvement.
    /// </summary>
    internal async Task TrackGoalExecutionAsync(AutonomousGoal goal, bool success, TimeSpan duration)
    {
        if (CapabilityRegistry == null) return;

        // Determine which capabilities were used
        var usedCapabilities = InferCapabilitiesFromGoal(goal.Description);

        foreach (var capName in usedCapabilities)
        {
            var result = CreateCapabilityPlanExecutionResult(success, duration, goal.Description);
            await CapabilityRegistry.UpdateCapabilityAsync(capName, result);
        }
    }

    /// <summary>
    /// Infers which capabilities were used based on goal description.
    /// </summary>
    internal static List<string> InferCapabilitiesFromGoal(string description)
    {
        var caps = new List<string> { "natural_language" };
        var lower = description.ToLowerInvariant();

        if (lower.Contains("|") || lower.Contains("pipeline") || lower.Contains("dsl"))
            caps.Add("pipeline_execution");
        if (lower.Contains("plan") || lower.Contains("step") || lower.Contains("multi"))
            caps.Add("planning");
        if (lower.Contains("tool") || lower.Contains("search") || lower.Contains("fetch"))
            caps.Add("tool_use");
        if (lower.Contains("metta") || lower.Contains("query") || lower.Contains("symbol"))
            caps.Add("symbolic_reasoning");
        if (lower.Contains("remember") || lower.Contains("recall") || lower.Contains("memory"))
            caps.Add("memory_management");
        if (lower.Contains("code") || lower.Contains("program") || lower.Contains("script"))
            caps.Add("coding");

        return caps;
    }

    /// <summary>
    /// Creates an PlanExecutionResult for capability tracking purposes.
    /// This creates a minimal valid PlanExecutionResult with empty plan/steps.
    /// </summary>
    internal static PlanExecutionResult CreateCapabilityPlanExecutionResult(bool success, TimeSpan duration, string taskDescription)
    {
        var minimalPlan = new Plan(
            Goal: taskDescription,
            Steps: new List<PlanStep>(),
            ConfidenceScores: new Dictionary<string, double>(),
            CreatedAt: DateTime.UtcNow);

        return new PlanExecutionResult(
            Plan: minimalPlan,
            StepResults: new List<StepResult>(),
            Success: success,
            FinalOutput: taskDescription,
            Metadata: new Dictionary<string, object>
            {
                ["capability_tracking"] = true,
                ["timestamp"] = DateTime.UtcNow
            },
            Duration: duration);
    }

    /// <summary>
    /// Reifies goal execution into the network state (MerkleDag).
    /// </summary>
    internal void ReifyGoalExecution(AutonomousGoal goal, string result, bool success, TimeSpan duration)

    {
        if (NetworkTracker == null) return;

        // Create a synthetic branch for goal execution tracking
        var store = new TrackedVectorStore();
        var dataSource = DataSource.FromPath(".");
        var branch = new PipelineBranch($"goal-exec-{goal.Id.ToString()[..8]}", store, dataSource);

        // Add goal execution event
        branch = branch.WithIngestEvent(
            $"goal:{(success ? "success" : "failure")}",
            new[] { goal.Description, result, duration.TotalSeconds.ToString("F2") });

        NetworkTracker.TrackBranch(branch);
        NetworkTracker.UpdateBranch(branch);
    }

    /// <summary>
    /// Executes a goal autonomously using planning and sub-agent delegation.
    /// </summary>
    internal async Task<string> ExecuteGoalAutonomouslyAsync(AutonomousGoal goal)
    {
        var sb = new StringBuilder();

        // Step 1: Plan the goal
        if (Orchestrator != null)
        {
            var planResult = await Orchestrator.PlanAsync(goal.Description);
            if (planResult.IsSuccess)
            {
                var plan = planResult.Value;
                sb.AppendLine($"Plan created with {plan.Steps.Count} steps");

                // Step 2: Check if we should delegate to sub-agents
                if (plan.Steps.Count > 3 && DistributedOrchestrator != null)
                {
                    // Distribute to sub-agents
                    var execResult = await DistributedOrchestrator.ExecuteDistributedAsync(plan);
                    if (execResult.IsSuccess)
                    {
                        sb.AppendLine($"Distributed execution completed: {execResult.Value.FinalOutput}");
                        return sb.ToString();
                    }
                }

                // Step 3: Execute directly
                var directResult = await Orchestrator.ExecuteAsync(plan);
                if (directResult.IsSuccess)
                {
                    sb.AppendLine($"Execution completed: {directResult.Value.FinalOutput}");
                }
                else
                {
                    sb.AppendLine($"Execution failed: {directResult.Error}");
                }
            }
            else
            {
                sb.AppendLine($"Planning failed: {planResult.Error}");
            }
        }
        else
        {
            // Fall back to simple chat-based execution
            var response = await ChatAsyncFunc($"Please help me accomplish this goal: {goal.Description}");
            sb.AppendLine(response);
        }

        return sb.ToString();
    }
}
