// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Services;
using Ouroboros.Application.Tools;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Resources;
using Ouroboros.Domain;
using PipelineGoal = Ouroboros.Pipeline.Planning.Goal;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

/// <summary>
/// Chat subsystem: owns the full LLM chat pipeline — prompt construction,
/// smart tool selection, post-processing, thought persistence, and learning.
/// </summary>
public sealed class ChatSubsystem : IChatSubsystem
{
    public string Name => "Chat";
    public bool IsInitialized { get; private set; }

    private OuroborosConfig _config = null!;
    private IConsoleOutput _output = null!;
    private VoiceModeService _voiceService = null!;
    private ModelSubsystem _modelsSub = null!;
    private ToolSubsystem _toolsSub = null!;
    private MemorySubsystem _memorySub = null!;
    private EmbodimentSubsystem _embodimentSub = null!;
    private CognitiveSubsystem _cognitiveSub = null!;
    private AutonomySubsystem _autonomySub = null!;

    // Tracking state (previously agent-local fields)
    private string? _lastUserInput;
    private DateTime _lastInteractionStart;

    // High-priority integrated systems (resolved from DI via RegisterEngineInterfaces)
    private Ouroboros.Pipeline.Memory.IEpisodicMemoryEngine? _episodicMemory;
    private readonly Ouroboros.Pipeline.Metacognition.MetacognitiveReasoner _metacognition = new();
    private Ouroboros.Agent.NeuralSymbolic.INeuralSymbolicBridge? _neuralSymbolicBridge;
    private Ouroboros.Core.Reasoning.ICausalReasoningEngine _causalReasoning = new Ouroboros.Core.Reasoning.CausalReasoningEngine();
    private Ouroboros.Agent.MetaAI.ICuriosityEngine? _curiosityEngine;
    private int _responseCount;
    private Ouroboros.CLI.Sovereignty.PersonaSovereigntyGate? _sovereigntyGate;

    // Delegates wired by agent during WireCrossSubsystemDependencies
    internal Func<InnerThought, string?, Task> PersistThoughtFunc { get; set; } =
        (_, _) => Task.CompletedTask;
    internal Func<Guid, string, string, bool, double, Task> PersistThoughtResultFunc { get; set; } =
        (_, _, _, _, _) => Task.CompletedTask;
    internal Func<string, string> GetLanguageNameFunc { get; set; } =
        culture => culture;

    public Task InitializeAsync(SubsystemInitContext ctx)
    {
        _config = ctx.Config;
        _output = ctx.Output;
        _voiceService = ctx.Voice.Service;
        _modelsSub = ctx.Models;
        _toolsSub = ctx.Tools;
        _memorySub = ctx.Memory;
        _embodimentSub = ctx.Embodiment;
        _cognitiveSub = ctx.Cognitive;
        _autonomySub = ctx.Autonomy;
        IsInitialized = true;

        // ── Engine interfaces (resolved from DI — registered in RegisterEngineInterfaces) ──
        _episodicMemory = ctx.Services?.GetService<Ouroboros.Pipeline.Memory.IEpisodicMemoryEngine>();
        _causalReasoning = ctx.Services?.GetService<Ouroboros.Core.Reasoning.ICausalReasoningEngine>()
            ?? new Ouroboros.Core.Reasoning.CausalReasoningEngine();

        // ── Neural-symbolic bridge ────────────────────────────────────────────
        if (ctx.Models.ChatModel != null && ctx.Memory.MeTTaEngine != null)
        {
            try
            {
                var kb = new Ouroboros.Agent.NeuralSymbolic.SymbolicKnowledgeBase(ctx.Memory.MeTTaEngine);
                _neuralSymbolicBridge = new Ouroboros.Agent.NeuralSymbolic.NeuralSymbolicBridge(ctx.Models.ChatModel, kb);
            }
            catch { }
        }

        // ── Curiosity engine ──────────────────────────────────────────────────
        if (ctx.Models.ChatModel != null && ctx.Memory.Skills != null)
        {
            try
            {
                var ethics = Ouroboros.Core.Ethics.EthicsFrameworkFactory.CreateDefault();
                var memStore = new Ouroboros.Agent.MetaAI.MemoryStore(ctx.Models.Embedding);
                var safetyGuard = new Ouroboros.Agent.MetaAI.SafetyGuard(
                    Ouroboros.Agent.MetaAI.PermissionLevel.Read, ctx.Memory.MeTTaEngine);
                _curiosityEngine = new Ouroboros.Agent.MetaAI.CuriosityEngine(
                    ctx.Models.ChatModel, memStore, ctx.Memory.Skills, safetyGuard, ethics);

                // Iaret's sovereignty gate — master control
                if (ctx.Models.ChatModel != null)
                {
                    try { _sovereigntyGate = new Ouroboros.CLI.Sovereignty.PersonaSovereigntyGate(ctx.Models.ChatModel); }
                    catch { }
                }

                var mind = ctx.Autonomy.AutonomousMind;
                if (mind != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            while (true)
                            {
                                await Task.Delay(TimeSpan.FromSeconds(90)).ConfigureAwait(false);
                                if (await _curiosityEngine.ShouldExploreAsync().ConfigureAwait(false))
                                {
                                    var opps = await _curiosityEngine
                                        .IdentifyExplorationOpportunitiesAsync(2).ConfigureAwait(false);
                                    foreach (var opp in opps)
                                    {
                                        if (_sovereigntyGate != null)
                                        {
                                            var v = await _sovereigntyGate
                                                .EvaluateExplorationAsync(opp.Description)
                                                .ConfigureAwait(false);
                                            if (!v.Approved) continue;
                                        }
                                        mind.InjectTopic(opp.Description);
                                    }
                                }
                            }
                        }
                        catch { }
                    });
                }
            }
            catch { }
        }

        ctx.Output.RecordInit("Chat", true, "pipeline ready");
        return Task.CompletedTask;
    }

    public async Task<string> ChatAsync(string input)
    {
        var activeLlm = _modelsSub.Llm;
        if (activeLlm == null)
        {
            var effectiveModel = (IChatCompletionModel?)_modelsSub.OrchestratedModel ?? _modelsSub.ChatModel;
            if (effectiveModel != null)
            {
                activeLlm = new ToolAwareChatModel(effectiveModel, _toolsSub.Tools);
                _modelsSub.Llm = activeLlm;
            }
        }

        if (activeLlm == null)
            return "I need an LLM connection to chat. Check if Ollama is running.";

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

        string toolInstruction = BuildToolInstruction(input);

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
                        episodicContext = $"\n[EPISODIC MEMORY — recalled from similar past exchanges]\n" +
                            string.Join("\n", summaries.Select(s => $"  • {s}")) +
                            "\n[END EPISODIC]\n";
                }
            }
            catch { }
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
            catch { }
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
            catch { }
        }

        _lastUserInput = input;
        _lastInteractionStart = DateTime.UtcNow;

        string prompt = $"{languageDirective}{costAwarenessPrompt}{toolAvailabilityStatement}{personalityPrompt}{persistentThoughtContext}{qdrantReflectiveContext}{episodicContext}{hybridContext}{causalContext}{toolInstruction}{injectedContext}\n\nRecent conversation:\n{context}\n\nUser: {input}\n\n{_voiceService.ActivePersona.Name}:";

        try
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

            string response;
            List<ToolExecution> tools;
            using (var spinner = _output.StartSpinner("Thinking..."))
            {
                (response, tools) = await activeLlm.GenerateWithToolsAsync(prompt);
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
            var expectedTools = _toolsSub.PromptOptimizer.DetectExpectedTools(input);
            var actualToolCalls = _toolsSub.PromptOptimizer.ExtractToolCalls(response);
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
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.WriteLine($"\n  ✧ [metacognition] Q={reflection.QualityScore:F2} " +
                    $"| {(reflection.HasIssues ? reflection.Improvements.FirstOrDefault() ?? "–" : "Clean")}");
                Console.ResetColor();
            }

            // ── Store episode ─────────────────────────────────────────────────
            if (_episodicMemory != null)
                _ = StoreAgentEpisodeAsync(input, response, ExtractTopicFromResponse(input));

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
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var topic = ExtractTopicFromResponse(input);
                            var mood = _memorySub.ValenceMonitor?.GetCurrentState().Valence > 0.5 ? "positive" : "neutral";
                            await _memorySub.PersonalityEngine.StoreConversationMemoryAsync(
                                _voiceService.ActivePersona.Name,
                                input, response, topic, mood, 0.6);
                        }
                        catch { }
                    });
                }

                if (_autonomySub.Coordinator?.IsActive == true && !string.IsNullOrWhiteSpace(input))
                {
                    _autonomySub.Coordinator.Network?.Broadcast(
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
        catch (Exception ex)
        {
            return $"I had trouble processing that: {ex.Message}";
        }
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
        catch
        {
            return $"{originalResponse}\n\n{toolResults}";
        }
    }

    private string BuildToolInstruction(string input)
    {
        if (_toolsSub.Tools.Count == 0) return string.Empty;

        var relevantTools = SelectRelevantTools(input);

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

        var selectionResult = TrySmartToolSelection(input);
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

    private List<ITool> SelectRelevantTools(string input)
    {
        var result = TrySmartToolSelection(input);
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

    private (List<ITool> tools, string reasoning)? TrySmartToolSelection(string input)
    {
        if (_toolsSub.SmartToolSelector == null || _toolsSub.ToolCapabilityMatcher == null)
            return null;

        try
        {
            var goal = PipelineGoal.Atomic(input, _ => true);
            var selectionResult = _toolsSub.SmartToolSelector.SelectForGoalAsync(goal).GetAwaiter().GetResult();
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
                sb.AppendLine($"  • {mem}");

            foreach (var msg in messages)
            {
                var content = msg.Payload?.ToString() ?? "";
                var label = string.IsNullOrWhiteSpace(msg.Topic) ? "" : $"[{msg.Topic}] ";
                sb.AppendLine($"  • {label}{content}");
            }

            sb.AppendLine("[END REFLECTIVE MEMORY]\n");
            return sb.ToString();
        }
        catch
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
            var ctx = Ouroboros.Pipeline.Memory.ExecutionContext.WithGoal(
                $"{_voiceService.ActivePersona.Name}: {input[..Math.Min(80, input.Length)]}");
            var outcome = Ouroboros.Pipeline.Memory.Outcome.Successful("Agent chat turn", TimeSpan.Zero);
            var metadata = System.Collections.Immutable.ImmutableDictionary<string, object>.Empty
                .Add("summary", $"Q: {input[..Math.Min(60, input.Length)]} → {response[..Math.Min(60, response.Length)]}")
                .Add("persona", _voiceService.ActivePersona.Name)
                .Add("topic", topic);
            await _episodicMemory.StoreEpisodeAsync(branch, ctx, outcome, metadata).ConfigureAwait(false);
        }
        catch { }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // Causal extraction and graph building consolidated in SharedAgentBootstrap.
    // Call sites use SharedAgentBootstrap.TryExtractCausalTerms / BuildMinimalCausalGraph.
}
