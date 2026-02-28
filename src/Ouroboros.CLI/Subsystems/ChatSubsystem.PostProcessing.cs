// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using Ouroboros.Application.Extensions;
using Ouroboros.Application.Services;
using Ouroboros.Application.Streams;
using Ouroboros.Application.Tools;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Resources;
using Ouroboros.Domain;
using Spectre.Console;

/// <summary>
/// Partial class containing post-processing, sanitization, episode storage,
/// and utility helpers.
/// </summary>
public sealed partial class ChatSubsystem
{
    /// <summary>
    /// Processes the LLM response: cognitive stream emissions, post-processing tool calls,
    /// prompt optimizer recording, metacognitive tracing, episode storage, and thought persistence.
    /// </summary>
    private async Task<string> ProcessLlmResponseAsync(string input, string response, List<ToolExecution> tools)
    {
        // === COGNITIVE STREAM: emit interesting tool executions ===
        if (CognitiveStreamEngine != null && tools.Count > 0)
        {
            foreach (var t in tools.Where(t => t.ToolName is "verify_claim" or "reasoning_chain" or
                    "parallel_metta_think" or "ouroboros_metta" or "episodic_memory"))
            {
                CognitiveStreamEngine.EmitToolExecution(t.ToolName, t.Output);
            }
        }

        // === POST-PROCESS: Execute tools when LLM talks about using them but doesn't ===
        if (tools.Count == 0)
        {
            var (enhancedResponse, executedTools) = await _toolsSub.PostProcessResponseForTools(response, input);
            if (executedTools.Count > 0)
            {
                response = enhancedResponse;
                tools = executedTools;
            }
        }

        // === RECORD OUTCOME FOR PROMPT OPTIMIZER ===
        var expectedTools = PromptOptimizer.DetectExpectedTools(input);
        var actualToolCalls = PromptOptimizer.ExtractToolCalls(response);
        actualToolCalls.AddRange(tools.Select(t => t.ToolName).Where(n => !actualToolCalls.Contains(n)));

        var wasSuccessful = expectedTools.Count == 0 || actualToolCalls.Count > 0;
        var outcome = new InteractionOutcome(
            input,
            response,
            expectedTools,
            actualToolCalls.Distinct().ToList(),
            wasSuccessful,
            DateTime.UtcNow - _lastInteractionStart);

        _toolsSub.PromptOptimizer.RecordOutcome(outcome);

        if (!wasSuccessful && expectedTools.Count > 0)
            System.Diagnostics.Debug.WriteLine($"[PromptOptimizer] Expected tools {string.Join(",", expectedTools)} but got none - learning from failure");

        _cognitiveSub.RecordInteractionForLearning(input, response);
        _cognitiveSub.RecordCognitiveEvent(input, response, tools);

        // ── End metacognitive trace ───────────────────────────────────────
        _metacognition.AddStep(Ouroboros.Pipeline.Metacognition.ReasoningStepType.Conclusion,
            response[..Math.Min(80, response.Length)], "LLM response");
        var traceResult = _metacognition.EndTrace(response[..Math.Min(40, response.Length)], true);
        _responseCount++;
        if (_responseCount % 5 == 0 && traceResult.IsSuccess)
        {
            var reflection = _metacognition.ReflectOn(traceResult.Value);
            var metaMsg = $"\n  \u2727 [[metacognition]] Q={reflection.QualityScore:F2} " +
                $"| {(reflection.HasIssues ? Markup.Escape(reflection.Improvements.FirstOrDefault() ?? "\u2013") : "Clean")}";
            AnsiConsole.MarkupLine($"[rgb(128,0,180)]{metaMsg}[/]");
        }

        // ── Store episode ─────────────────────────────────────────────────
        if (_episodicMemory != null)
            StoreAgentEpisodeAsync(input, response, ExtractTopicFromResponse(input)).ObserveExceptions("StoreAgentEpisode");

        if (!string.IsNullOrWhiteSpace(response))
        {
            var thought = InnerThought.CreateAutonomous(
                InnerThoughtType.Observation,
                $"User asked about '{TruncateForThought(input)}'. I responded with thoughts about {ExtractTopicFromResponse(response)}.",
                confidence: 0.8,
                priority: ThoughtPriority.Normal);
            _ = PersistThoughtFunc(thought, ExtractTopicFromResponse(input));
            _ = PersistThoughtResultFunc(
                thought.Id,
                Ouroboros.Domain.Persistence.ThoughtResult.Types.Response,
                TruncateForThought(response, 500),
                true,
                0.85);

            if (_memorySub.PersonalityEngine != null && _memorySub.PersonalityEngine.HasMemory)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        var topic = ExtractTopicFromResponse(input);
                        var mood = _memorySub.ValenceMonitor?.GetCurrentState().Valence > 0.5 ? "positive" : "neutral";
                        await _memorySub.PersonalityEngine.StoreConversationMemoryAsync(
                            _voiceService.ActivePersona.Name,
                            input, response, topic, mood, 0.6);
                    }
                    catch (HttpRequestException) { }
                })
                .ObserveExceptions("StoreConversationMemory");
            }

            if (_autonomySub.Coordinator?.IsActive == true && !string.IsNullOrWhiteSpace(input))
            {
                _ = _autonomySub.Coordinator.Network?.BroadcastAsync(
                    "learning.fact",
                    $"User interaction: {TruncateForThought(input, 100)} -> {TruncateForThought(response, 100)}",
                    "chat");
            }
        }

        if (tools?.Any() == true)
        {
            string toolResults = string.Join("\n", tools.Select(t => $"[{t.ToolName}]: {t.Output}"));

            foreach (var tool in tools)
            {
                var isSuccessful = !string.IsNullOrEmpty(tool.Output) && !tool.Output.StartsWith("Error");
                var toolThought = InnerThought.CreateAutonomous(
                    InnerThoughtType.Strategic,
                    $"Executed tool '{tool.ToolName}' with result: {TruncateForThought(tool.Output, 200)}",
                    confidence: isSuccessful ? 0.9 : 0.4,
                    priority: ThoughtPriority.High);
                _ = PersistThoughtResultFunc(
                    toolThought.Id,
                    Ouroboros.Domain.Persistence.ThoughtResult.Types.Action,
                    $"Tool: {tool.ToolName}, Output: {TruncateForThought(tool.Output, 300)}",
                    isSuccessful,
                    isSuccessful ? 0.9 : 0.4);
            }

            return await SanitizeToolResultsAsync(response, toolResults);
        }

        return ToolSubsystem.DetectAndCorrectToolMisinformation(response);
    }

    public async Task<string> SanitizeToolResultsAsync(string originalResponse, string toolResults)
    {
        if (_modelsSub.ChatModel == null || string.IsNullOrWhiteSpace(toolResults))
            return $"{originalResponse}\n\n{toolResults}";

        try
        {
            var sanitized = await _modelsSub.ChatModel.GenerateTextAsync(
                PromptResources.ToolIntegration(originalResponse, toolResults));
            return string.IsNullOrWhiteSpace(sanitized)
                ? $"{originalResponse}\n\n{toolResults}"
                : sanitized;
        }
        catch (HttpRequestException)
        {
            return $"{originalResponse}\n\n{toolResults}";
        }
    }

    private static string TruncateForThought(string text, int maxLength = 50)
    {
        if (string.IsNullOrWhiteSpace(text)) return "unknown topic";
        return text.Length > maxLength ? text[..maxLength] + "..." : text;
    }

    private static string ExtractTopicFromResponse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "general discussion";

        var firstSentence = text.Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (firstSentence != null && firstSentence.Length <= 80)
            return firstSentence.Trim();

        return text.Length > 60 ? text[..60] + "..." : text;
    }

    private async Task StoreAgentEpisodeAsync(string input, string response, string topic)
    {
        if (_episodicMemory == null) return;
        try
        {
            var store = new Ouroboros.Domain.Vectors.TrackedVectorStore();
            var dataSource = LangChain.DocumentLoaders.DataSource.FromPath(System.Environment.CurrentDirectory);
            var branch = new Ouroboros.Pipeline.Branches.PipelineBranch("agent_chat", store, dataSource);
            var ctx = Ouroboros.Pipeline.Memory.PipelineExecutionContext.WithGoal(
                $"{_voiceService.ActivePersona.Name}: {input[..Math.Min(80, input.Length)]}");
            var outcome = Ouroboros.Pipeline.Memory.Outcome.Successful("Agent chat turn", TimeSpan.Zero);
            var metadata = System.Collections.Immutable.ImmutableDictionary<string, object>.Empty
                .Add("summary", $"Q: {input[..Math.Min(60, input.Length)]} \u2192 {response[..Math.Min(60, response.Length)]}")
                .Add("persona", _voiceService.ActivePersona.Name)
                .Add("topic", topic);
            await _episodicMemory.StoreEpisodeAsync(branch, ctx, outcome, metadata).ConfigureAwait(false);
        }
        catch (HttpRequestException) { }
    }

    // Causal extraction and graph building consolidated in SharedAgentBootstrap.
    // Call sites use SharedAgentBootstrap.TryExtractCausalTerms / BuildMinimalCausalGraph.
}
