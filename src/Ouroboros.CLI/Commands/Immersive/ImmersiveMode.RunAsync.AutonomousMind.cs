// <copyright file="ImmersiveMode.RunAsync.AutonomousMind.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.CLI.Commands;

using Ouroboros.Application.Personality.Consciousness;
using Ouroboros.Application.Tools;
using Ouroboros.CLI.Infrastructure;
using Spectre.Console;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

public sealed partial class ImmersiveMode
{
    /// <summary>
    /// Configures the autonomous mind: think/pipeline/persist/search/tool/sanitize functions,
    /// wires up limitation-busting tool delegates, and subscribes to mind events.
    /// </summary>
    private void ConfigureAutonomousMind()
    {
        _autonomousMind = new AutonomousMind();
        _autonomousMind.ThinkFunction = async (prompt, token) =>
        {
            // Use orchestration for autonomous thinking - routes to appropriate model
            return await GenerateWithOrchestrationAsync(prompt, useDivideAndConquer: false, token);
        };

        // Wire up pipeline-based reasoning function for monadic thinking
        _autonomousMind.PipelineThinkFunction = async (prompt, existingBranch, token) =>
        {
            // Use orchestration for pipeline-based thinking
            var response = await GenerateWithOrchestrationAsync(prompt, useDivideAndConquer: false, token);

            // If we have a branch, add the thought as a reasoning event
            if (existingBranch != null)
            {
                var updatedBranch = existingBranch.WithReasoning(
                    new Ouroboros.Domain.States.Thinking(response),
                    prompt,
                    null);
                return (response, updatedBranch);
            }

            return (response, existingBranch!);
        };

        // Wire up state persistence functions
        _autonomousMind.PersistLearningFunction = async (category, content, confidence, token) =>
        {
            if (_networkStateProjector != null)
            {
                await _networkStateProjector.RecordLearningAsync(
                    category,
                    content,
                    "autonomous_mind",
                    confidence,
                    token);
            }
        };

        _autonomousMind.PersistEmotionFunction = async (emotion, token) =>
        {
            if (_networkStateProjector != null)
            {
                await _networkStateProjector.RecordLearningAsync(
                    "emotional_state",
                    $"Emotion: {emotion.DominantEmotion} (arousal={emotion.Arousal:F2}, valence={emotion.Valence:F2}) - {emotion.Description}",
                    "autonomous_mind",
                    0.6,
                    token);
            }
        };

        _autonomousMind.SearchFunction = async (query, token) =>
        {
            var searchTool = _dynamicToolFactory?.CreateWebSearchTool("duckduckgo");
            if (searchTool != null)
            {
                var result = await searchTool.InvokeAsync(query, token);
                return result.Match(s => s, e => "");
            }
            return "";
        };
        _autonomousMind.ExecuteToolFunction = async (toolName, input, token) =>
        {
            var tool = _dynamicTools.Get(toolName);
            if (tool != null)
            {
                var result = await tool.InvokeAsync(input, token);
                return result.Match(s => s, e => $"Error: {e}");
            }
            return "Tool not found";
        };

        // Sanitize raw outputs through LLM for natural language
        _autonomousMind.SanitizeOutputFunction = async (rawOutput, token) =>
        {
            var model = _orchestratedModel ?? _baseModel;
            if (model == null || string.IsNullOrWhiteSpace(rawOutput))
                return rawOutput;

            try
            {
                string prompt = $@"Summarize this in ONE brief, natural sentence (max 50 words). No markdown:
{rawOutput}";

                string sanitized = await model.GenerateTextAsync(prompt, token);
                return string.IsNullOrWhiteSpace(sanitized) ? rawOutput : sanitized.Trim();
            }
            catch
            {
                return rawOutput;
            }
        };
    }

    /// <summary>
    /// Wires limitation-busting tool delegates (VerifyClaimTool, ReasoningChainTool, etc.)
    /// to the current model and autonomous mind functions.
    /// </summary>
    private void WireLimitationBustingTools()
    {
        VerifyClaimTool.SearchFunction = _autonomousMind!.SearchFunction;
        VerifyClaimTool.EvaluateFunction = async (prompt, token) =>
        {
            var model = _orchestratedModel ?? _baseModel;
            return model != null ? await model.GenerateTextAsync(prompt, token) : "";
        };
        ReasoningChainTool.ReasonFunction = async (prompt, token) =>
        {
            var model = _orchestratedModel ?? _baseModel;
            return model != null ? await model.GenerateTextAsync(prompt, token) : "";
        };
        ParallelToolsTool.ExecuteToolFunction = _autonomousMind.ExecuteToolFunction;
        CompressContextTool.SummarizeFunction = async (prompt, token) =>
        {
            var model = _orchestratedModel ?? _baseModel;
            return model != null ? await model.GenerateTextAsync(prompt, token) : "";
        };
        SelfDoubtTool.CritiqueFunction = async (prompt, token) =>
        {
            var model = _orchestratedModel ?? _baseModel;
            return model != null ? await model.GenerateTextAsync(prompt, token) : "";
        };
        ParallelMeTTaThinkTool.OllamaFunction = async (prompt, token) =>
        {
            var model = _orchestratedModel ?? _baseModel;
            return model != null ? await model.GenerateTextAsync(prompt, token) : "";
        };
        OuroborosMeTTaTool.OllamaFunction = async (prompt, token) =>
        {
            var model = _orchestratedModel ?? _baseModel;
            return model != null ? await model.GenerateTextAsync(prompt, token) : "";
        };
    }

    /// <summary>
    /// Subscribes to autonomous mind events (console output for proactive messages,
    /// thoughts, discoveries, emotional changes, and state persistence).
    /// </summary>
    private void SubscribeAutonomousMindEvents()
    {
        _autonomousMind!.OnProactiveMessage += (msg) =>
        {
            string savedInput;
            lock (_inputLock)
            {
                savedInput = _currentInputBuffer.ToString();
            }

            // Clear current line and show proactive message
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[rgb(128,0,180)]{Markup.Escape($"[Autonomous] {msg}")}[/]");

            // Restore prompt and any text user was typing
            AnsiConsole.Markup($"\n{OuroborosTheme.Warn(_currentPromptPrefix)}");
            if (!string.IsNullOrEmpty(savedInput))
            {
                AnsiConsole.Markup(Markup.Escape(savedInput));
            }
        };
        _autonomousMind.OnThought += (thought) =>
        {
            System.Diagnostics.Debug.WriteLine($"[Thought] {thought.Type}: {thought.Content}");
        };
        _autonomousMind.OnDiscovery += (query, fact) =>
        {
            System.Diagnostics.Debug.WriteLine($"[Discovery] {query}: {fact}");
        };
        _autonomousMind.OnEmotionalChange += (emotion) =>
        {
            AnsiConsole.MarkupLine($"\n  [rgb(148,103,189)]{Markup.Escape($"[mind] Emotional shift: {emotion.DominantEmotion} ({emotion.Description})")}[/]");
        };
        _autonomousMind.OnStatePersisted += (msg) =>
        {
            System.Diagnostics.Debug.WriteLine($"[State] {msg}");
        };
    }
}
