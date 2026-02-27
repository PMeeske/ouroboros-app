// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using System.Text;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Services;
using Ouroboros.Application.Tools;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Resources;
using PipelineGoal = Ouroboros.Pipeline.Planning.Goal;

/// <summary>
/// Partial class containing prompt building and tool selection helpers.
/// </summary>
public sealed partial class ChatSubsystem
{
    /// <summary>
    /// Builds the full chat prompt from all context sources (auto-tool injection,
    /// conversation history, Qdrant memory, episodic memory, neural-symbolic reasoning,
    /// causal reasoning, personality memory, etc.).
    /// </summary>
    private async Task<string> BuildChatPromptAsync(string input)
    {
        // === PRE-PROCESS: Auto-inject tool calls for knowledge-seeking questions ===
        string autoToolResult = await _toolsSub.TryAutoToolExecution(input);
        string injectedContext = "";
        if (!string.IsNullOrEmpty(autoToolResult))
        {
            injectedContext = $@"
[AUTOMATICALLY RETRIEVED CONTEXT]
{autoToolResult}
[END AUTO CONTEXT]

Use this actual code information to answer the user's question accurately.
";
        }

        string context = string.Join("\n", _memorySub.ConversationHistory.TakeLast(6));

        // Retrieve semantically related past thoughts/answers from Qdrant for reflection
        string qdrantReflectiveContext = await BuildQdrantReflectiveContextAsync(input);

        string languageDirective = string.Empty;
        if (!string.IsNullOrEmpty(_config.Culture) && _config.Culture != "en-US")
        {
            var languageName = GetLanguageNameFunc(_config.Culture);
            languageDirective = PromptResources.LanguageDirective(languageName, _config.Culture) + "\n\n";
        }

        string costAwarenessPrompt = _config.CostAware
            ? LlmCostTracker.GetCostAwarenessPrompt(_config.Model) + "\n\n"
            : string.Empty;

        string toolAvailabilityStatement = _toolsSub.Tools.Count > 0
            ? PromptResources.ToolAvailability(_toolsSub.Tools.Count)
            : "";

        string embodimentContext = _embodimentSub.BodySchema != null
            ? $"\n\nPHYSICAL EMBODIMENT:\n{_embodimentSub.BodySchema.DescribeSelf()}"
            : "";

        string personalityPrompt = _voiceService.BuildPersonalityPrompt(
            $"Available skills: {_memorySub.Skills?.GetAllSkills().Count() ?? 0}\nAvailable tools: {_toolsSub.Tools.Count}{embodimentContext}");

        string persistentThoughtContext = BuildPersistentThoughtContext();
        string cognitiveStreamContext = CognitiveStreamEngine?.BuildContextBlock() ?? string.Empty;

        string toolInstruction = await BuildToolInstructionAsync(input);

        // ── Episodic retrieval ────────────────────────────────────────────────
        string episodicContext = "";
        if (_episodicMemory != null)
        {
            try
            {
                var eps = await _episodicMemory.RetrieveSimilarEpisodesAsync(
                    input, topK: 3, minSimilarity: 0.65).ConfigureAwait(false);
                if (eps.IsSuccess && eps.Value.Count > 0)
                {
                    var summaries = eps.Value
                        .Select(e => e.Context.GetValueOrDefault("summary")?.ToString())
                        .Where(s => !string.IsNullOrEmpty(s)).Take(2).ToList();
                    if (summaries.Count > 0)
                        episodicContext = $"\n[EPISODIC MEMORY \u2014 recalled from similar past exchanges]\n" +
                            string.Join("\n", summaries.Select(s => $"  \u2022 {s}")) +
                            "\n[END EPISODIC]\n";
                }
            }
            catch (HttpRequestException) { }
        }

        // ── Metacognitive trace ───────────────────────────────────────────────
        _metacognition.StartTrace();
        _metacognition.AddStep(Ouroboros.Pipeline.Metacognition.ReasoningStepType.Observation,
            $"Input: {input[..Math.Min(80, input.Length)]}", "User query received");

        // ── Neural-symbolic hybrid reasoning ─────────────────────────────────
        string hybridContext = "";
        bool isComplexQuery = input.Contains('?') ||
            input.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 10;
        if (_neuralSymbolicBridge != null && isComplexQuery)
        {
            try
            {
                var hybrid = await _neuralSymbolicBridge.HybridReasonAsync(
                    input, Ouroboros.Agent.NeuralSymbolic.ReasoningMode.SymbolicFirst)
                    .ConfigureAwait(false);
                if (hybrid.IsSuccess && !string.IsNullOrEmpty(hybrid.Value.Answer))
                {
                    hybridContext = $"\n[SYMBOLIC REASONING]\n" +
                        $"{hybrid.Value.Answer[..Math.Min(200, hybrid.Value.Answer.Length)]}" +
                        "\n[END SYMBOLIC]\n";
                    _metacognition.AddStep(Ouroboros.Pipeline.Metacognition.ReasoningStepType.Inference,
                        hybridContext[..Math.Min(80, hybridContext.Length)], "Neural-symbolic bridge");
                }
            }
            catch (HttpRequestException) { }
        }

        // ── Causal reasoning ─────────────────────────────────────────────────
        string causalContext = "";
        var causalTerms = Services.SharedAgentBootstrap.TryExtractCausalTerms(input);
        if (causalTerms.HasValue)
        {
            try
            {
                var graph = Services.SharedAgentBootstrap.BuildMinimalCausalGraph(causalTerms.Value.Cause, causalTerms.Value.Effect);
                var explanation = await _causalReasoning.ExplainCausallyAsync(
                    causalTerms.Value.Effect, [causalTerms.Value.Cause], graph)
                    .ConfigureAwait(false);
                if (explanation.IsSuccess && !string.IsNullOrEmpty(explanation.Value.NarrativeExplanation))
                {
                    causalContext = $"\n[CAUSAL ANALYSIS]\n" +
                        $"{explanation.Value.NarrativeExplanation[..Math.Min(200, explanation.Value.NarrativeExplanation.Length)]}" +
                        "\n[END CAUSAL]\n";
                    _metacognition.AddStep(Ouroboros.Pipeline.Metacognition.ReasoningStepType.Inference,
                        causalContext[..Math.Min(80, causalContext.Length)], "Causal reasoning");
                }
            }
            catch (HttpRequestException) { }
        }

        // ── Personality conversation memory (recalled past conversations from Qdrant) ──
        string conversationMemoryContext = "";
        if (_memorySub.PersonalityEngine != null && _memorySub.PersonalityEngine.HasMemory)
        {
            try
            {
                conversationMemoryContext = await _memorySub.PersonalityEngine.GetMemoryContextAsync(
                    input, _voiceService.ActivePersona.Name, 3).ConfigureAwait(false);
            }
            catch (HttpRequestException) { }
        }

        return $"{languageDirective}{costAwarenessPrompt}{toolAvailabilityStatement}{personalityPrompt}{persistentThoughtContext}{cognitiveStreamContext}{qdrantReflectiveContext}{conversationMemoryContext}{episodicContext}{hybridContext}{causalContext}{toolInstruction}{injectedContext}\n\nRecent conversation:\n{context}\n\nUser: {input}\n\n{_voiceService.ActivePersona.Name}:";
    }

    /// <summary>
    /// Attempts to detect a new person from the input.
    /// </summary>
    private async Task TryDetectPersonAsync(string input)
    {
        if (_memorySub.PersonalityEngine != null && _memorySub.PersonalityEngine.HasMemory)
        {
            try
            {
                var detectionResult = await _memorySub.PersonalityEngine.DetectPersonAsync(input);
                if (detectionResult.IsNewPerson && detectionResult.Person.Name != null)
                    System.Diagnostics.Debug.WriteLine($"[PersonDetection] New person detected: {detectionResult.Person.Name}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PersonDetection] Error: {ex.Message}");
            }
        }
    }

    private async Task<string> BuildToolInstructionAsync(string input)
    {
        if (_toolsSub.Tools.Count == 0) return string.Empty;

        var relevantTools = await SelectRelevantToolsAsync(input);

        var simpleTools = relevantTools
            .Where(t => t.Name != "playwright")
            .Select(t => $"{t.Name} ({t.Description})")
            .ToList();

        bool hasFirecrawl = _toolsSub.Tools.All.Any(t => t.Name == "web_research");
        string primarySearchTool = hasFirecrawl ? "web_research" : "duckduckgo_search";
        string primarySearchDesc = hasFirecrawl
            ? "Deep web research with Firecrawl (PREFERRED for research)"
            : "Basic web search";
        string searchExample = hasFirecrawl
            ? "[TOOL:web_research ouroboros mythology symbol]"
            : "[TOOL:duckduckgo_search ouroboros mythology symbol]";

        string toolInstruction = PromptResources.ToolUsageInstruction(
            primarySearchTool, primarySearchDesc,
            searchExample, string.Join(", ", simpleTools.Take(5)));

        var selectionResult = await TrySmartToolSelectionAsync(input);
        if (selectionResult.HasValue && !string.IsNullOrEmpty(selectionResult.Value.reasoning)
            && relevantTools.Count < _toolsSub.Tools.Count)
        {
            toolInstruction += PromptResources.SmartToolHint(
                string.Join(", ", relevantTools.Select(t => t.Name)),
                selectionResult.Value.reasoning);
        }

        string optimizedSection = _toolsSub.PromptOptimizer.GenerateOptimizedToolInstruction(
            relevantTools.Select(t => t.Name).ToList(), input);
        return toolInstruction + $"\n\n{optimizedSection}";
    }

    private async Task<List<ITool>> SelectRelevantToolsAsync(string input)
    {
        var result = await TrySmartToolSelectionAsync(input);
        List<ITool> relevantTools = result?.tools ?? [];

        if (relevantTools.Count == 0)
            relevantTools = _toolsSub.Tools.All.ToList();

        var criticalToolNames = new HashSet<string> { "modify_my_code", "read_my_file", "search_my_code", "rebuild_self" };
        foreach (var name in criticalToolNames)
        {
            var tool = _toolsSub.Tools.All.FirstOrDefault(t => t.Name == name);
            if (tool != null && !relevantTools.Any(t => t.Name == name))
                relevantTools.Add(tool);
        }

        return relevantTools;
    }

    private async Task<(List<ITool> tools, string reasoning)?> TrySmartToolSelectionAsync(string input)
    {
        if (_toolsSub.SmartToolSelector == null || _toolsSub.ToolCapabilityMatcher == null)
            return null;

        try
        {
            var goal = PipelineGoal.Atomic(input, _ => true);
            var selectionResult = await _toolsSub.SmartToolSelector.SelectForGoalAsync(goal);
            if (selectionResult.IsSuccess && selectionResult.Value.HasTools)
            {
                return (selectionResult.Value.SelectedTools.ToList(), selectionResult.Value.Reasoning);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SmartToolSelector] Error: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Searches Qdrant for semantically related past thoughts, memories, and answers
    /// and formats them as a reflective context block for the LLM prompt.
    /// </summary>
    private async Task<string> BuildQdrantReflectiveContextAsync(string input)
    {
        var memory = _memorySub.NeuralMemory;
        if (memory?.EmbedFunction == null) return "";

        try
        {
            var embedding = await memory.EmbedFunction(input, CancellationToken.None);

            // Search stored memories and past message threads in parallel
            var memoriesTask = memory.SearchMemoriesAsync(embedding, limit: 4);
            var messagesTask = memory.SearchSimilarMessagesAsync(embedding, limit: 4);
            await Task.WhenAll(memoriesTask, messagesTask);

            var memories = memoriesTask.Result.Where(m => !string.IsNullOrWhiteSpace(m)).Take(3).ToList();
            var messages = messagesTask.Result
                .Where(m => !string.IsNullOrWhiteSpace(m.Payload?.ToString()))
                .Take(3)
                .ToList();

            if (memories.Count == 0 && messages.Count == 0) return "";

            var sb = new StringBuilder();
            sb.AppendLine("\n[REFLECTIVE MEMORY — related thoughts and past answers from your memory]");

            foreach (var mem in memories)
                sb.AppendLine($"  \u2022 {mem}");

            foreach (var msg in messages)
            {
                var content = msg.Payload?.ToString() ?? "";
                var label = string.IsNullOrWhiteSpace(msg.Topic) ? "" : $"[{msg.Topic}] ";
                sb.AppendLine($"  \u2022 {label}{content}");
            }

            sb.AppendLine("[END REFLECTIVE MEMORY]\n");
            return sb.ToString();
        }
        catch (HttpRequestException)
        {
            return "";
        }
    }

    private string BuildPersistentThoughtContext()
    {
        var thoughts = _memorySub.PersistentThoughts;
        if (thoughts.Count == 0) return "";

        var sb = new StringBuilder();
        sb.AppendLine("\n[PERSISTENT MEMORY - Your thoughts from previous sessions]");

        foreach (var thought in thoughts.OrderByDescending(t => t.Timestamp).Take(10))
        {
            var age = DateTime.UtcNow - thought.Timestamp;
            var ageStr = age.TotalHours < 1 ? $"{age.TotalMinutes:F0}m ago"
                       : age.TotalDays < 1 ? $"{age.TotalHours:F0}h ago"
                       : $"{age.TotalDays:F0}d ago";
            sb.AppendLine($"  [{thought.Type}] ({ageStr}): {thought.Content}");
        }

        sb.AppendLine("[END PERSISTENT MEMORY]\n");
        return sb.ToString();
    }
}
