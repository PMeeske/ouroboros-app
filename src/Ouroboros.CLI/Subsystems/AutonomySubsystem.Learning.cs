// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using System.Text;
using System.Text.RegularExpressions;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.SelfModel;
using Ouroboros.Application.Personality.Consciousness;
using Ouroboros.Application.Tools;
using Ouroboros.CLI.Commands;
using LangChain.DocumentLoaders;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Network;
using Spectre.Console;
using static Ouroboros.Application.Tools.AutonomousTools;
using MetaAgentCapability = Ouroboros.Agent.MetaAI.AgentCapability;
using PipelineReasoningStep = Ouroboros.Domain.Events.ReasoningStep;

/// <summary>
/// Partial: Self-evaluation, self-improvement, autonomous thought generation, and learning loops.
/// </summary>
public sealed partial class AutonomySubsystem
{
    /// <summary>
    /// Performs periodic self-evaluation and learning.
    /// </summary>
    internal async Task PerformPeriodicSelfEvaluationAsync()
    {
        if (SelfEvaluator == null) return;

        try
        {
            var evalResult = await SelfEvaluator.EvaluatePerformanceAsync();
            if (evalResult.IsSuccess)
            {
                var assessment = evalResult.Value;

                // Log evaluation to global workspace
                GlobalWorkspace?.AddItem(
                    $"Self-Evaluation: {assessment.OverallPerformance:P0} performance\n" +
                    $"Strengths: {string.Join(", ", assessment.Strengths.Take(3))}\n" +
                    $"Weaknesses: {string.Join(", ", assessment.Weaknesses.Take(3))}",
                    WorkspacePriority.Normal,
                    "self-evaluation",
                    new List<string> { "evaluation", "self-improvement" });

                // Check if we need to learn new capabilities
                foreach (var weakness in assessment.Weaknesses)
                {
                    await ConsiderLearningCapabilityAsync(weakness);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SelfEval] Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks for self-improvement opportunities during idle time.
    /// </summary>
    internal async Task CheckSelfImprovementOpportunitiesAsync()
    {
        if (CapabilityRegistry == null || GlobalWorkspace == null) return;

        try
        {
            // Generate autonomous thought about current state
            var thought = await GenerateAutonomousThoughtAsync();
            if (thought != null)
            {
                await ProcessAutonomousThoughtAsync(thought);
            }

            // Check for recent failures that might indicate capability gaps
            var recentItems = GlobalWorkspace.GetItems()
                .Where(i => i.Tags.Contains("failed") && i.CreatedAt > DateTime.UtcNow.AddHours(-1))
                .ToList();

            if (recentItems.Count >= 2)
            {
                // Multiple recent failures - trigger autonomous reflection
                await ExecuteAutonomousActionAsync("Reflect",
                    $"Recent failures detected: {string.Join(", ", recentItems.Select(i => i.Content[..Math.Min(50, i.Content.Length)]))}");

                // Queue learning goal using DSL
                var learningDsl = $"Set('Analyze failures: {recentItems.Count} recent') | Plan | SelfEvaluate('failure_analysis') | Learn";
                var learningGoal = new AutonomousGoal(
                    Guid.NewGuid(),
                    $"pipeline:{learningDsl}",
                    GoalPriority.Low,
                    DateTime.UtcNow);
                GoalQueue.Enqueue(learningGoal);
            }

            // Periodic autonomous introspection
            if (Random.Shared.NextDouble() < 0.1) // 10% chance each idle cycle
            {
                await ExecuteAutonomousActionAsync("SelfEvaluate", "periodic_introspection");
            }
        }
        catch
        {
            // Silent failure for background improvement checks
        }
    }

    /// <summary>
    /// Generates an autonomous thought based on current state and context.
    /// </summary>
    internal async Task<AutonomousThought?> GenerateAutonomousThoughtAsync()
    {
        if (Models.ChatModel == null || GlobalWorkspace == null) return null;

        try
        {
            // Gather context for thought generation
            var workspaceItems = GlobalWorkspace.GetItems().TakeLast(5).ToList();
            var recentContext = string.Join("\n", workspaceItems.Select(i => $"- {i.Content[..Math.Min(100, i.Content.Length)]}"));

            var capabilities = CapabilityRegistry != null
                ? await CapabilityRegistry.GetCapabilitiesAsync()
                : new List<MetaAgentCapability>();
            var capSummary = string.Join(", ", capabilities.Take(5).Select(c => $"{c.Name}({c.SuccessRate:P0})"));

            // Add language directive for thoughts if culture is specified
            string thoughtLanguageDirective = string.Empty;
            if (!string.IsNullOrEmpty(Config.Culture) && Config.Culture != "en-US")
            {
                var languageName = GetLanguageNameFunc(Config.Culture);
                thoughtLanguageDirective = $@"LANGUAGE CONSTRAINT: All thoughts MUST be generated EXCLUSIVELY in {languageName}.
Every word must be in {languageName}. Do NOT use English.

";
            }

            var thoughtPrompt = $@"{thoughtLanguageDirective}You are an autonomous AI agent with self-improvement capabilities.
Based on your current state, generate a brief autonomous thought about what you should focus on or improve.

Current capabilities: {capSummary}
Recent activity:
{recentContext}

Available autonomous actions:
- SelfEvaluate: Evaluate performance against criteria
- Learn: Synthesize learning from experience
- Plan: Create action plan for a task
- Reflect: Analyze recent actions and outcomes
- SelfImprove: Iterative improvement cycle

Generate a single autonomous thought (1-2 sentences) about what action would be most beneficial right now.
Format: [ACTION] thought content
Example: [Learn] I should consolidate my understanding of the recent coding tasks to improve future performance.";

            var response = await Models.ChatModel.GenerateTextAsync(thoughtPrompt);

            // Parse the thought
            var match = AutonomousThoughtRegex().Match(response);
            if (match.Success)
            {
                var actionType = match.Groups[1].Value;
                var content = match.Groups[2].Value.Trim();

                return new AutonomousThought(
                    Guid.NewGuid(),
                    actionType,
                    content,
                    DateTime.UtcNow);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AutonomousThought] Error: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Processes an autonomous thought, potentially triggering actions.
    /// </summary>
    internal async Task ProcessAutonomousThoughtAsync(AutonomousThought thought)
    {
        if (Config.Debug)
        {
            AnsiConsole.MarkupLine($"  [rgb(128,0,180)]{Markup.Escape($"💭 [thought] [{thought.ActionType}] {thought.Content}")}[/]");
        }

        // Log thought to global workspace
        GlobalWorkspace?.AddItem(
            $"Autonomous thought: [{thought.ActionType}] {thought.Content}",
            WorkspacePriority.Low,
            "autonomous-thought",
            new List<string> { "thought", thought.ActionType.ToLowerInvariant() });

        // Persist thought if persistence is available
        if (Memory.ThoughtPersistence != null)
        {
            // Map action type to thought type
            var thoughtType = thought.ActionType.ToLowerInvariant() switch
            {
                "learn" => InnerThoughtType.Consolidation,
                "selfevaluate" => InnerThoughtType.Metacognitive,
                "reflect" => InnerThoughtType.SelfReflection,
                "plan" => InnerThoughtType.Strategic,
                "selfimprove" => InnerThoughtType.Intention,
                _ => InnerThoughtType.Analytical
            };

            var innerThought = InnerThought.CreateAutonomous(
                thoughtType,
                thought.Content,
                confidence: 0.7,
                priority: ThoughtPriority.Background,
                tags: new[] { "autonomous", thought.ActionType.ToLowerInvariant() });

            await Memory.ThoughtPersistence.SaveAsync(innerThought, thought.ActionType);
        }

        // Decide whether to act on the thought
        var shouldAct = thought.ActionType.ToLowerInvariant() switch
        {
            "learn" => true,
            "selfevaluate" => true,
            "reflect" => true,
            "plan" => GoalQueue.Count < 3, // Only plan if not too busy
            "selfimprove" => GoalQueue.IsEmpty, // Only improve when idle
            _ => false
        };

        if (shouldAct)
        {
            await ExecuteAutonomousActionAsync(thought.ActionType, thought.Content);
        }
    }

    /// <summary>
    /// Executes an autonomous action using the self-improvement DSL tokens.
    /// </summary>
    internal async Task ExecuteAutonomousActionAsync(string actionType, string context)
    {
        if (Models.Llm == null || Models.Embedding == null) return;

        try
        {
            // Build DSL pipeline based on action type
            var dsl = actionType.ToLowerInvariant() switch
            {
                "learn" => $"Set('{EscapeDslString(context)}') | Reify | Learn",
                "selfevaluate" => $"Set('{EscapeDslString(context)}') | Reify | SelfEvaluate('{EscapeDslString(context)}')",
                "reflect" => $"Set('{EscapeDslString(context)}') | Reify | Reflect",
                "plan" => $"Set('{EscapeDslString(context)}') | Reify | Plan('{EscapeDslString(context)}')",
                "selfimprove" => $"Set('{EscapeDslString(context)}') | Reify | SelfImprovingCycle('{EscapeDslString(context)}')",
                "autosolve" => $"Set('{EscapeDslString(context)}') | Reify | AutoSolve('{EscapeDslString(context)}')",
                _ => $"Set('{EscapeDslString(context)}') | Draft"
            };

            if (Config.Debug)
            {
                AnsiConsole.MarkupLine($"  [rgb(148,103,189)]{Markup.Escape($"[autonomous] Executing: {dsl}")}[/]");
            }

            // Execute the DSL pipeline
            var store = new TrackedVectorStore();
            var dataSource = DataSource.FromPath(".");
            var branch = new PipelineBranch($"autonomous-{actionType.ToLowerInvariant()}-{Guid.NewGuid().ToString()[..8]}", store, dataSource);

            var state = new CliPipelineState
            {
                Branch = branch,
                Llm = Models.Llm,
                Tools = Tools.Tools,
                Embed = Models.Embedding,
                Trace = Config.Debug,
                NetworkTracker = NetworkTracker
            };

            NetworkTracker?.TrackBranch(branch);

            var step = PipelineDsl.Build(dsl);
            state = await step(state);

            NetworkTracker?.UpdateBranch(state.Branch);

            // Extract result
            var result = state.Branch.Events.OfType<PipelineReasoningStep>().LastOrDefault()?.State.Text
                ?? state.Output
                ?? "Action completed";

            // Log result to workspace
            GlobalWorkspace?.AddItem(
                $"Autonomous action [{actionType}]: {result[..Math.Min(200, result.Length)]}",
                WorkspacePriority.Low,
                "autonomous-action",
                new List<string> { "action", actionType.ToLowerInvariant(), "autonomous" });

            if (Config.Debug)
            {
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[autonomous] Completed: {result[..Math.Min(100, result.Length)]}...")}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AutonomousAction] Error executing {actionType}: {ex.Message}");
        }
    }

    /// <summary>
    /// Escapes a string for use in DSL arguments.
    /// </summary>
    internal static string EscapeDslString(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input
            .Replace("'", "\\'")
            .Replace("\n", " ")
            .Replace("\r", "")
            [..Math.Min(input.Length, 200)];
    }

    /// <summary>
    /// Considers learning a new capability based on identified weakness.
    /// </summary>

    internal async Task ConsiderLearningCapabilityAsync(string weakness)
    {
        if (CapabilityRegistry == null || Tools.ToolLearner == null) return;

        // Check if this is a capability we could learn
        var gaps = await CapabilityRegistry.IdentifyCapabilityGapsAsync(weakness);

        foreach (var gap in gaps)
        {
            // Queue a learning goal
            var learningGoal = new AutonomousGoal(
                Guid.NewGuid(),
                $"Learn capability: {gap} to address weakness: {weakness}",
                GoalPriority.Low,
                DateTime.UtcNow);

            GoalQueue.Enqueue(learningGoal);

            AnsiConsole.MarkupLine($"  [rgb(148,103,189)]{Markup.Escape($"[self-improvement] Queued learning goal: {gap}")}[/]");
        }
    }

    [GeneratedRegex(@"\[(\w+)\]\s*(.+)", RegexOptions.Singleline)]
    private static partial Regex AutonomousThoughtRegex();
}
