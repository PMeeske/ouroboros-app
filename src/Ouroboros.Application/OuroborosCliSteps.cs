// <copyright file="OuroborosCliSteps.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Text;
using Ouroboros.Abstractions;
using Ouroboros.Agent.MetaAI;

// Alias to avoid naming conflict with the OuroborosConfidence method
using ConfidenceLevel = Ouroboros.Agent.MetaAI.OuroborosConfidence;

namespace Ouroboros.Application;

/// <summary>
/// CLI pipeline steps for Ouroboros self-improving AI orchestration.
/// Provides access to the recursive Plan-Execute-Verify-Learn cycle through pipeline DSL.
/// </summary>
public static partial class OuroborosCliSteps
{
    // Thread-local storage for the current Ouroboros atom
    private static OuroborosAtom? _currentAtom;

    /// <summary>
    /// Initializes an Ouroboros atom for self-improving orchestration.
    /// Usage: OuroborosInit or OuroborosInit('MyAgent')
    /// </summary>
    [PipelineToken("OuroborosInit", "InitOuroboros", "SelfInit")]
    public static Step<CliPipelineState, CliPipelineState> OuroborosInit(string? args = null)
        => s =>
        {
            string name = ParseString(args);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Ouroboros";
            }

            _currentAtom = OuroborosAtom.CreateDefault(name);

            if (s.Trace)
            {
                Console.WriteLine($"[ouroboros] Initialized: {_currentAtom.Name} ({_currentAtom.InstanceId})");
                Console.WriteLine($"[ouroboros] Capabilities: {_currentAtom.Capabilities.Count}");
                Console.WriteLine($"[ouroboros] Current Phase: {_currentAtom.CurrentPhase}");
            }

            s.Output = $"Ouroboros '{name}' initialized with {_currentAtom.Capabilities.Count} capabilities";
            s.Branch = s.Branch.WithIngestEvent($"ouroboros:init:{_currentAtom.InstanceId}", Array.Empty<string>());

            return Task.FromResult(s);
        };

    /// <summary>
    /// Sets a goal for the Ouroboros to pursue.
    /// Usage: OuroborosGoal('Analyze and improve code quality')
    /// </summary>
    [PipelineToken("OuroborosGoal", "SetGoal", "SelfGoal")]
    public static Step<CliPipelineState, CliPipelineState> OuroborosGoal(string? args = null)
        => s =>
        {
            if (_currentAtom == null)
            {
                Console.WriteLine("[ouroboros] Not initialized. Call OuroborosInit first.");
                return Task.FromResult(s);
            }

            string goal = ParseString(args);
            if (string.IsNullOrWhiteSpace(goal))
            {
                goal = !string.IsNullOrWhiteSpace(s.Query) ? s.Query : s.Prompt;
            }

            if (string.IsNullOrWhiteSpace(goal))
            {
                Console.WriteLine("[ouroboros] No goal provided");
                return Task.FromResult(s);
            }

            // Check if goal is safe
            if (!_currentAtom.IsSafeAction(goal))
            {
                Console.WriteLine($"[ouroboros] ⚠ Goal violates safety constraints: {goal}");
                s.Output = "Goal rejected due to safety constraints";
                return Task.FromResult(s);
            }

            _currentAtom.SetGoal(goal);

            if (s.Trace)
            {
                Console.WriteLine($"[ouroboros] Goal set: {goal}");
                Console.WriteLine($"[ouroboros] Confidence: {_currentAtom.AssessConfidence(goal)}");
            }

            s.Query = goal;
            s.Output = $"Goal set: {goal}";
            s.Branch = s.Branch.WithIngestEvent($"ouroboros:goal:{goal}", Array.Empty<string>());

            return Task.FromResult(s);
        };

    /// <summary>
    /// Advances to the next phase in the improvement cycle.
    /// Usage: OuroborosAdvance
    /// </summary>
    [PipelineToken("OuroborosAdvance", "AdvancePhase", "NextPhase")]
    public static Step<CliPipelineState, CliPipelineState> OuroborosAdvance(string? args = null)
        => s =>
        {
            if (_currentAtom == null)
            {
                Console.WriteLine("[ouroboros] Not initialized. Call OuroborosInit first.");
                return Task.FromResult(s);
            }

            ImprovementPhase previousPhase = _currentAtom.CurrentPhase;
            ImprovementPhase newPhase = _currentAtom.AdvancePhase();

            if (s.Trace)
            {
                Console.WriteLine($"[ouroboros] Phase transition: {previousPhase} -> {newPhase}");
                Console.WriteLine($"[ouroboros] Cycles completed: {_currentAtom.CycleCount}");
            }

            s.Output = $"Phase: {previousPhase} -> {newPhase}";

            // If we completed a cycle, log it
            if (newPhase == ImprovementPhase.Plan && previousPhase == ImprovementPhase.Learn)
            {
                Console.WriteLine($"[ouroboros] ✓ Cycle {_currentAtom.CycleCount} completed!");
                s.Branch = s.Branch.WithIngestEvent($"ouroboros:cycle:{_currentAtom.CycleCount}", Array.Empty<string>());
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Performs self-reflection - the Ouroboros examines its own state.
    /// Usage: OuroborosReflect
    /// </summary>
    [PipelineToken("OuroborosReflect", "SelfReflect", "Introspect")]
    public static Step<CliPipelineState, CliPipelineState> OuroborosReflect(string? args = null)
        => s =>
        {
            if (_currentAtom == null)
            {
                Console.WriteLine("[ouroboros] Not initialized. Call OuroborosInit first.");
                return Task.FromResult(s);
            }

            string reflection = _currentAtom.SelfReflect();

            Console.WriteLine(reflection);

            s.Output = reflection;
            s.Context = reflection;

            return Task.FromResult(s);
        };

    /// <summary>
    /// Assesses confidence for a given action.
    /// Usage: OuroborosConfidence('planning a complex task')
    /// </summary>
    [PipelineToken("OuroborosConfidence", "AssessConfidence", "CheckConfidence")]
    public static Step<CliPipelineState, CliPipelineState> OuroborosConfidence(string? args = null)
        => s =>
        {
            if (_currentAtom == null)
            {
                Console.WriteLine("[ouroboros] Not initialized. Call OuroborosInit first.");
                return Task.FromResult(s);
            }

            string action = ParseString(args);
            if (string.IsNullOrWhiteSpace(action))
            {
                action = s.Query;
            }

            if (string.IsNullOrWhiteSpace(action))
            {
                Console.WriteLine("[ouroboros] No action to assess");
                return Task.FromResult(s);
            }

            ConfidenceLevel confidenceLevel = _currentAtom.AssessConfidence(action);

            string confidenceMessage = confidenceLevel switch
            {
                ConfidenceLevel.High => "High - can proceed confidently",
                ConfidenceLevel.Medium => "Medium - proceed with verification",
                ConfidenceLevel.Low => "Low - requires careful planning and validation",
                _ => "Unknown"
            };

            if (s.Trace)
            {
                Console.WriteLine($"[ouroboros] Action: {action}");
                Console.WriteLine($"[ouroboros] Confidence: {confidenceMessage}");
            }

            s.Output = $"Confidence for '{action}': {confidenceMessage}";

            return Task.FromResult(s);
        };

    private static string ParseString(string? arg)
    {
        arg ??= string.Empty;
        if (arg.StartsWith("'") && arg.EndsWith("'") && arg.Length >= 2)
        {
            return arg[1..^1];
        }

        if (arg.StartsWith("\"") && arg.EndsWith("\"") && arg.Length >= 2)
        {
            return arg[1..^1];
        }

        return arg;
    }

    private static string TruncateString(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text.Length <= maxLength ? text : text[..(maxLength - 3)] + "...";
    }
}
