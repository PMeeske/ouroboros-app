// <copyright file="ImmersiveMode.Response.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.CLI.Commands;

using System.Text;
using Ouroboros.Abstractions.Agent;
using Ouroboros.Agent;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Application;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Personality.Consciousness;
using Ouroboros.Application.Services;
using Ouroboros.Application.Tools;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Core.DistinctionLearning;
using Ouroboros.Domain.DistinctionLearning;
using Ouroboros.Options;
using Ouroboros.Providers;
using static Ouroboros.Application.Tools.AutonomousTools;
using Ouroboros.Abstractions;
using Spectre.Console;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

public sealed partial class ImmersiveMode
{
    private bool _llmMessagePrinted = false;

    private async Task<IChatCompletionModel> CreateChatModelAsync(IVoiceOptions options)
    {
        // If subsystems are configured, return the pre-initialized effective model
        if (HasSubsystems && _modelsSub != null)
        {
            var effective = _modelsSub.GetEffectiveModel();
            if (effective != null)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Ok("  [OK] Using LLM from agent subsystem"));
                return effective;
            }
        }

        var settings = new ChatRuntimeSettings(0.8, 1024, 120, false);

        // Try remote CHAT_ENDPOINT if configured
        string? endpoint = Environment.GetEnvironmentVariable("CHAT_ENDPOINT");
        string? apiKey = Environment.GetEnvironmentVariable("CHAT_API_KEY");

        IChatCompletionModel baseModel;

        if (!string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(apiKey))
        {
            if (!_llmMessagePrinted)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Dim($"  [~] Using remote LLM: {options.Model} via {endpoint}"));
                _llmMessagePrinted = true;
            }
            baseModel = new HttpOpenAiCompatibleChatModel(endpoint, apiKey, options.Model, settings);
        }
        else
        {
            // Use Ollama cloud model with the configured endpoint
            if (!_llmMessagePrinted)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Dim($"  [~] Using Ollama LLM: {options.Model} via {options.Endpoint}"));
                _llmMessagePrinted = true;
            }
            baseModel = new OllamaCloudChatModel(options.Endpoint, "ollama", options.Model, settings);
        }

        // Store base model for orchestration
        _baseModel = baseModel;

        // Initialize multi-model orchestration if specialized models are configured via environment
        await InitializeImmersiveOrchestrationAsync(options, settings, endpoint, apiKey);

        // Return orchestrated model if available, otherwise base model
        return _orchestratedModel ?? baseModel;
    }

    /// <summary>
    /// Initializes multi-model orchestration for immersive mode.
    /// Uses environment variables for specialized model configuration.
    /// </summary>
    private async Task InitializeImmersiveOrchestrationAsync(
        IVoiceOptions options,
        ChatRuntimeSettings settings,
        string? endpoint,
        string? apiKey)
    {
        try
        {
            // Check for specialized models via environment variables
            var coderModel = Environment.GetEnvironmentVariable("IMMERSIVE_CODER_MODEL");
            var reasonModel = Environment.GetEnvironmentVariable("IMMERSIVE_REASON_MODEL");
            var summarizeModel = Environment.GetEnvironmentVariable("IMMERSIVE_SUMMARIZE_MODEL");

            bool hasSpecializedModels = !string.IsNullOrEmpty(coderModel)
                                     || !string.IsNullOrEmpty(reasonModel)
                                     || !string.IsNullOrEmpty(summarizeModel);

            if (!hasSpecializedModels || _baseModel == null)
            {
                return; // No orchestration needed
            }

            bool isLocal = string.IsNullOrEmpty(endpoint) || endpoint.Contains("localhost");

            // Helper to create a model
            IChatCompletionModel CreateModel(string modelName)
            {
                if (isLocal)
                    return new OllamaCloudChatModel(options.Endpoint, "ollama", modelName, settings);
                return new HttpOpenAiCompatibleChatModel(endpoint!, apiKey ?? "", modelName, settings);
            }

            // Build orchestrated chat model
            var builder = new OrchestratorBuilder(_dynamicTools, "general")
                .WithModel(
                    "general",
                    _baseModel,
                    ModelType.General,
                    new[] { "conversation", "general-purpose", "versatile", "chat", "emotion", "consciousness" },
                    maxTokens: 1024,
                    avgLatencyMs: 1000);

            if (!string.IsNullOrEmpty(coderModel))
            {
                builder.WithModel(
                    "coder",
                    CreateModel(coderModel),
                    ModelType.Code,
                    new[] { "code", "programming", "debugging", "tool", "script" },
                    maxTokens: 2048,
                    avgLatencyMs: 1500);
                AnsiConsole.MarkupLine(OuroborosTheme.Dim($"  [~] Multi-model: Coder = {coderModel}"));
            }

            if (!string.IsNullOrEmpty(reasonModel))
            {
                builder.WithModel(
                    "reasoner",
                    CreateModel(reasonModel),
                    ModelType.Reasoning,
                    new[] { "reasoning", "analysis", "introspection", "planning", "philosophy" },
                    maxTokens: 2048,
                    avgLatencyMs: 1200);
                AnsiConsole.MarkupLine(OuroborosTheme.Dim($"  [~] Multi-model: Reasoner = {reasonModel}"));
            }

            if (!string.IsNullOrEmpty(summarizeModel))
            {
                builder.WithModel(
                    "summarizer",
                    CreateModel(summarizeModel),
                    ModelType.General,
                    new[] { "summarize", "condense", "memory", "recall" },
                    maxTokens: 1024,
                    avgLatencyMs: 800);
                AnsiConsole.MarkupLine(OuroborosTheme.Dim($"  [~] Multi-model: Summarizer = {summarizeModel}"));
            }

            builder.WithMetricTracking(true);
            _orchestratedModel = builder.Build();

            // Initialize divide-and-conquer for large input processing
            var dcConfig = new DivideAndConquerConfig(
                MaxParallelism: Math.Max(2, Environment.ProcessorCount / 2),
                ChunkSize: 800,
                MergeResults: true,
                MergeSeparator: "\n\n");
            _divideAndConquer = new DivideAndConquerOrchestrator(_orchestratedModel, dcConfig);

            AnsiConsole.MarkupLine(OuroborosTheme.Ok("  [OK] Multi-model orchestration enabled for immersive mode"));

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  [!] Multi-model orchestration unavailable: {Markup.Escape(ex.Message)}"));
        }
    }

    /// <summary>
    /// Generates text using orchestration if available, with optional divide-and-conquer for large inputs.
    /// </summary>
    private async Task<string> GenerateWithOrchestrationAsync(
        string prompt,
        bool useDivideAndConquer = false,
        CancellationToken ct = default)
    {
        // For large inputs, use divide-and-conquer
        if (useDivideAndConquer && _divideAndConquer != null && prompt.Length > 2000)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Dim($"  [D&C] Processing large input ({prompt.Length} chars)..."));

            var chunks = _divideAndConquer.DivideIntoChunks(prompt);
            var dcResult = await _divideAndConquer.ExecuteAsync("Process:", chunks, ct);

            if (dcResult.IsSuccess)
                return dcResult.Value;

            // Fall back to direct generation on D&C failure
            return await ((_orchestratedModel ?? _baseModel)?.GenerateTextAsync(prompt, ct) ?? Task.FromResult(""));
        }

        // Use orchestrated model if available
        if (_orchestratedModel != null)
        {
            return await _orchestratedModel.GenerateTextAsync(prompt, ct);
        }

        // Fall back to base model
        return await (_baseModel?.GenerateTextAsync(prompt, ct) ?? Task.FromResult(""));
    }

    private async Task<string> GenerateImmersiveResponseAsync(
        ImmersivePersona persona,
        IChatCompletionModel chatModel,
        string input,
        List<(string Role, string Content)> history,
        CancellationToken ct)
    {
        var personaName = persona.Identity.Name;

        // --- Inner dialog pre-processing (AGI integration) ---
        // Run the persona's consciousness pipeline: inner dialog + Hyperon symbolic reasoning.
        // The insights produced here inform the final spoken response exactly as a person
        // would think before speaking. Non-fatal — falls back gracefully.
        List<string> innerThoughts = [];
        try
        {
            var preThought = await persona.RespondAsync(input, ct: ct);
            // Take the top 3 genuine inner thoughts (exclude symbolic processing notes)
            innerThoughts = preThought.InnerThoughts
                .Where(t => !t.StartsWith("symbolic-processing-note:", StringComparison.OrdinalIgnoreCase)
                         && !t.StartsWith("inference-available:", StringComparison.OrdinalIgnoreCase)
                         // Exclude AI-identity echoes — generic AI self-descriptions confuse the LLM
                         && !t.StartsWith("I am an AI", StringComparison.OrdinalIgnoreCase)
                         && !t.StartsWith("As an AI", StringComparison.OrdinalIgnoreCase)
                         && !t.Contains("I cannot answer", StringComparison.OrdinalIgnoreCase)
                         && !t.Contains("designed to provide helpful", StringComparison.OrdinalIgnoreCase))
                .Take(3)
                .ToList();
            if (!string.IsNullOrEmpty(preThought.CognitiveApproach) && preThought.CognitiveApproach != "direct engagement")
                innerThoughts.Add($"cognitive approach: {preThought.CognitiveApproach}");
        }
        catch (Exception)
        {
            // Non-fatal — inner dialog failure does not block the response
        }

        // ── Episodic retrieval ────────────────────────────────────────────────
        string? episodicContext = null;
        if (_episodicMemory != null)
        {
            try
            {
                var eps = await _episodicMemory.RetrieveSimilarEpisodesAsync(
                    input, topK: 3, minSimilarity: 0.65, ct).ConfigureAwait(false);
                if (eps.IsSuccess && eps.Value.Count > 0)
                {
                    var summaries = eps.Value
                        .Select(e => e.Context.GetValueOrDefault("summary")?.ToString())
                        .Where(s => !string.IsNullOrEmpty(s)).Take(2);
                    var joined = string.Join("; ", summaries);
                    if (!string.IsNullOrEmpty(joined))
                        episodicContext = $"[Recalled: {joined}]";
                }
            }
            catch (HttpRequestException) { }
        }

        // ── Metacognitive trace ───────────────────────────────────────────────
        _metacognition.StartTrace();
        _metacognition.AddStep(Ouroboros.Pipeline.Metacognition.ReasoningStepType.Observation,
            $"Input: {input[..Math.Min(80, input.Length)]}", "User query received");

        // ── Ethics gate ──────────────────────────────────────────────────────
        // Evaluate the user query before generating a response.
        // Void → refuse; Imaginary (requires approval) → add a caution note.
        string? ethicsCautionNote = null;
        if (_immersiveEthics != null)
        {
            try
            {
                var ethicsCheck = await _immersiveEthics.EvaluateActionAsync(
                    new Ouroboros.Core.Ethics.ProposedAction
                    {
                        ActionType   = "generate_response",
                        Description  = $"Respond to user input: {input[..Math.Min(120, input.Length)]}",
                        Parameters   = new Dictionary<string, object>
                        {
                            ["personaName"] = personaName,
                            ["inputLength"]  = input.Length,
                        },
                        PotentialEffects = ["Speak to the user", "Influence user's thinking"],
                    },
                    new Ouroboros.Core.Ethics.ActionContext
                    {
                        AgentId     = personaName,
                        Environment = "immersive_session",
                        State       = new Dictionary<string, object> { ["mode"] = "interactive" },
                    }, ct).ConfigureAwait(false);

                if (ethicsCheck.IsSuccess)
                {
                    if (!ethicsCheck.Value.IsPermitted)
                        return $"I'm unable to respond to that in this context. {ethicsCheck.Value.Reasoning}";
                    if (ethicsCheck.Value.Level == Ouroboros.Core.Ethics.EthicalClearanceLevel.RequiresHumanApproval)
                        ethicsCautionNote = $"[Ethical caution: {ethicsCheck.Value.Reasoning}]";
                }
            }
            catch (Exception) { /* Non-fatal — ethics check failure does not block response */ }
        }

        _metacognition.AddStep(Ouroboros.Pipeline.Metacognition.ReasoningStepType.Validation,
            ethicsCautionNote ?? "Ethics: clear", "MeTTa ethics evaluation");

        // ── CognitivePhysics shift ───────────────────────────────────────────
        // Compute context-shift cost when topic changes; surface as awareness in the prompt.
        string? cogPhysicsNote = null;
        if (_immersiveCogPhysics != null)
        {
            try
            {
                var topic = Subsystems.ImmersiveSubsystem.ClassifyAvatarTopic(input);
                if (string.IsNullOrEmpty(topic)) topic = _immersiveLastTopic;

                var shiftResult = await _immersiveCogPhysics.ExecuteTrajectoryAsync(
                    _immersiveCogState, [topic]).ConfigureAwait(false);

                if (shiftResult.IsSuccess)
                {
                    var prevTopic = _immersiveLastTopic;
                    _immersiveCogState = shiftResult.Value;
                    _immersiveLastTopic = topic;

                    double resourcePct = _immersiveCogState.Resources / 100.0;
                    if (prevTopic != topic && _immersiveCogState.Compression > 0.3)
                        cogPhysicsNote = $"(Conceptual leap: {prevTopic} → {topic}, " +
                                         $"resources at {resourcePct:P0}, " +
                                         $"compression={_immersiveCogState.Compression:F2})";
                }
            }
            catch (Exception) { /* Non-fatal */ }
        }

        _metacognition.AddStep(Ouroboros.Pipeline.Metacognition.ReasoningStepType.Inference,
            cogPhysicsNote ?? "No shift", "CognitivePhysics shift cost");

        // ── Neural-symbolic hybrid reasoning ─────────────────────────────────
        string? hybridNote = null;
        bool isComplexQuery = input.Contains('?') ||
            input.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 10;
        if (_neuralSymbolicBridge != null && isComplexQuery)
        {
            try
            {
                var hybrid = await _neuralSymbolicBridge.HybridReasonAsync(
                    input, Ouroboros.Agent.NeuralSymbolic.ReasoningMode.SymbolicFirst, ct)
                    .ConfigureAwait(false);
                if (hybrid.IsSuccess && !string.IsNullOrEmpty(hybrid.Value.Answer))
                    hybridNote = $"[Symbolic: {hybrid.Value.Answer[..Math.Min(120, hybrid.Value.Answer.Length)]}]";
            }
            catch (Exception) { }
            if (hybridNote != null)
                _metacognition.AddStep(Ouroboros.Pipeline.Metacognition.ReasoningStepType.Inference,
                    hybridNote, "Neural-symbolic bridge");
        }

        // ── Causal reasoning ─────────────────────────────────────────────────
        string? causalNote = null;
        var causalTerms = Services.SharedAgentBootstrap.TryExtractCausalTerms(input);
        if (causalTerms.HasValue)
        {
            try
            {
                var graph = Services.SharedAgentBootstrap.BuildMinimalCausalGraph(causalTerms.Value.Cause, causalTerms.Value.Effect);
                var explanation = await _causalReasoning.ExplainCausallyAsync(
                    causalTerms.Value.Effect, [causalTerms.Value.Cause], graph, ct)
                    .ConfigureAwait(false);
                if (explanation.IsSuccess && !string.IsNullOrEmpty(explanation.Value.NarrativeExplanation))
                {
                    causalNote = $"[Causal: {explanation.Value.NarrativeExplanation[..Math.Min(150, explanation.Value.NarrativeExplanation.Length)]}]";
                    _metacognition.AddStep(Ouroboros.Pipeline.Metacognition.ReasoningStepType.Inference,
                        causalNote, "Causal reasoning engine");
                }
            }
            catch (Exception) { }
        }

        // ── Phi (IIT) annotation ─────────────────────────────────────────────
        // Measure conversational integration across recent turns.
        // High Φ → prefer orchestrated model; Low Φ → fall back to base model.
        string? phiNote = null;
        try
        {
            var recentPairs = history.TakeLast(10).ToList();
            int userTurns = recentPairs.Count(t => t.Role == "user");
            int assistantTurns = recentPairs.Count(t => t.Role == "assistant");

            if (userTurns > 0 && assistantTurns > 0)
            {
                int total = userTurns + assistantTurns;
                var pathways = new List<Ouroboros.Providers.NeuralPathway>
                {
                    new() { Name = "user",      Synapses = total, Activations = userTurns,      Weight = 1.0 },
                    new() { Name = personaName, Synapses = total, Activations = assistantTurns, Weight = 1.0 },
                };
                var phiResult = _immersivePhiCalc.Compute(pathways);

                if (phiResult.Phi >= 0.5 && _orchestratedModel != null)
                    chatModel = _orchestratedModel; // Upgrade to collective model
                else if (phiResult.Phi < 0.2 && _baseModel != null)
                    chatModel = _baseModel;         // Keep single model for simple queries

                phiNote = $"Φ={phiResult.Phi:F2}";
            }
        }
        catch (Exception) { /* Non-fatal */ }

        var sb = new StringBuilder();
        sb.AppendLine("### System");

        // Full consciousness-aware system prompt from the persona
        sb.AppendLine(persona.GenerateSystemPrompt());

        // Language detection via LanguageSubsystem (aya-expanse:8b cloud model, heuristic fallback)
        var detectedLang = await Ouroboros.CLI.Subsystems.LanguageSubsystem
            .DetectStaticAsync(input, ct).ConfigureAwait(false);
        _lastDetectedCulture = detectedLang.Culture;
        AnsiConsole.MarkupLine($"[rgb(148,103,189)]  [[lang: {Markup.Escape(detectedLang.Language)} ({Markup.Escape(detectedLang.Culture)})]][/]");
        sb.AppendLine();
        if (detectedLang.Culture != "en-US")
            sb.AppendLine($"LANGUAGE INSTRUCTION: The user is writing in {detectedLang.Language}. Respond ENTIRELY in {detectedLang.Language}. Do not switch to English.");
        else
            sb.AppendLine("LANGUAGE INSTRUCTION: Respond in the same language as the user. If they switch languages mid-conversation, switch with them immediately.");

        // Inject inner dialog insights so the LLM speaks informed by prior thinking
        if (innerThoughts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("YOUR PRE-THOUGHTS (inner dialog and symbolic reasoning before you speak):");
            foreach (var t in innerThoughts)
                sb.AppendLine($"- {t}");
            sb.AppendLine("Let these inform your response naturally — do not list them explicitly.");
        }

        // Inject ethics caution (if any) and cognitive-physics / Phi awareness
        if (ethicsCautionNote != null || cogPhysicsNote != null || phiNote != null
            || episodicContext != null || hybridNote != null || causalNote != null)
        {
            sb.AppendLine();
            sb.AppendLine("COGNITIVE STATE:");
            if (ethicsCautionNote != null) sb.AppendLine($"- Ethics: {ethicsCautionNote}");
            if (cogPhysicsNote != null)    sb.AppendLine($"- Context shift: {cogPhysicsNote}");
            if (phiNote != null)           sb.AppendLine($"- Conversation integration: {phiNote}");
            if (episodicContext != null)   sb.AppendLine($"- Memory: {episodicContext}");
            if (hybridNote != null)        sb.AppendLine($"- Reasoning: {hybridNote}");
            if (causalNote != null)        sb.AppendLine($"- Causal: {causalNote}");
        }

        // Add pipeline context if relevant
        if (IsPipelineRelatedQuery(input) || !string.IsNullOrEmpty(_lastPipelineContext))
        {
            sb.AppendLine();
            sb.AppendLine("PIPELINE CONTEXT: When asked for examples, show real usage like:");
            sb.AppendLine("- ArxivSearch 'neural networks' | Summarize");
            sb.AppendLine("- WikiSearch 'quantum computing'");
            sb.AppendLine($"You have {_allTokens?.Count ?? 0} pipeline tokens available.");
            _lastPipelineContext = null;
        }
        sb.AppendLine();

        // Add recent history (includes current user input, deduplicated)
        // Strip the [From date]: prefix injected by PersistentConversationMemory for recalled sessions —
        // it is temporal metadata for our use, not a response format the LLM should mimic.
        string? lastContent = null;
        foreach (var (role, content) in history.TakeLast(8))
        {
            if (content == lastContent) continue;
            lastContent = content;

            var cleanContent = System.Text.RegularExpressions.Regex.Replace(
                content, @"^\[From [^\]]+\]:\s*", string.Empty);

            if (role == "user")
                sb.AppendLine($"### Human\n{cleanContent}");
            else
                sb.AppendLine($"### Assistant\n{cleanContent}");
        }

        sb.AppendLine();
        sb.AppendLine("### Assistant");

        var prompt = sb.ToString();

        try
        {
            var result = await chatModel.GenerateTextAsync(prompt, ct);

            // Debug: show raw response in gray
            if (!string.IsNullOrWhiteSpace(result))
            {
                var preview = result.Length > 80 ? result[..80] + "..." : result;
                AnsiConsole.MarkupLine(OuroborosTheme.Dim($"  \\[raw: {Markup.Escape(preview.Replace("\n", " "))}]"));
            }

            // Clean up the response
            var response = CleanResponse(result, personaName);

            // ── End metacognitive trace ───────────────────────────────────────
            _metacognition.AddStep(Ouroboros.Pipeline.Metacognition.ReasoningStepType.Conclusion,
                response[..Math.Min(80, response.Length)], "LLM response");
            var traceResult = _metacognition.EndTrace(response[..Math.Min(40, response.Length)], true);
            _immersiveResponseCount++;
            if (_immersiveResponseCount % 5 == 0 && traceResult.IsSuccess)
            {
                var reflection = _metacognition.ReflectOn(traceResult.Value);
                var metaMsg = $"  ✧ [[metacognition]] Q={reflection.QualityScore:F2} " +
                    $"| {(reflection.HasIssues ? Markup.Escape(reflection.Improvements.FirstOrDefault() ?? "–") : "Clean")}";
                AnsiConsole.MarkupLine($"\n[rgb(128,0,180)]{metaMsg}[/]");
            }

            if (_episodicMemory != null)
                _ = StoreConversationEpisodeAsync(_episodicMemory, input, response,
                    _immersiveLastTopic, personaName, CancellationToken.None);

            return response;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"  {IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned)} [red]{Markup.Escape($"[LLM error: {ex.Message}]")}[/]");
            return "I'm having trouble thinking right now. Let me try again.";
        }
    }

    private string CleanResponse(string raw, string personaName)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "I'm here. What would you like to talk about?";

        var response = raw.Trim();

        // Remove model fallback markers
        if (response.Contains("[ollama-fallback:"))
        {
            var markerEnd = response.IndexOf(']');
            if (markerEnd > 0 && markerEnd < response.Length - 1)
                response = response[(markerEnd + 1)..].Trim();
        }

        // If response contains "### Assistant" marker, extract only the content after it
        var assistantMarker = "### Assistant";
        var lastAssistantIdx = response.LastIndexOf(assistantMarker, StringComparison.OrdinalIgnoreCase);
        if (lastAssistantIdx >= 0)
        {
            response = response[(lastAssistantIdx + assistantMarker.Length)..].Trim();
        }

        // Remove any remaining ### markers
        response = System.Text.RegularExpressions.Regex.Replace(response, @"###\s*(System|Human|Assistant)\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();

        // If response contains prompt keywords, it's echoing the system prompt - provide fallback
        if (response.Contains("friendly AI companion", StringComparison.OrdinalIgnoreCase) ||
            response.Contains("Current mood:", StringComparison.OrdinalIgnoreCase) ||
            response.Contains("Keep responses concise", StringComparison.OrdinalIgnoreCase) ||
            response.StartsWith("You are " + personaName, StringComparison.OrdinalIgnoreCase) ||
            response.Contains("CORE IDENTITY:") ||
            response.Contains("BEHAVIORAL GUIDELINES:"))
        {
            return "Hey there! What's up?";
        }

        // Strip Iaret-as-AI self-introduction lines — the persona should never introduce herself as "an AI"
        // These are usually echoes from a confused model or safety-filter responses
        var selfIntroLines = response.Split('\n')
            .Where(l =>
            {
                var t = l.Trim();
                return !t.StartsWith("I am an AI", StringComparison.OrdinalIgnoreCase)
                    && !t.StartsWith("As an AI", StringComparison.OrdinalIgnoreCase)
                    && !t.StartsWith("I'm an AI", StringComparison.OrdinalIgnoreCase)
                    && !(t.StartsWith("I am " + personaName, StringComparison.OrdinalIgnoreCase)
                         && t.Length < 80); // short identity sentences like "I am Iaret, your AI companion"
            })
            .ToList();
        if (selfIntroLines.Count > 0)
            response = string.Join("\n", selfIntroLines).Trim();

        // Remove persona name prefix if echoed
        if (response.StartsWith($"{personaName}:", StringComparison.OrdinalIgnoreCase))
            response = response[(personaName.Length + 1)..].Trim();

        // Remove "Human:" lines that might be echoed back
        var lines = response.Split('\n');
        var cleanedLines = lines.Where(l =>
            !l.TrimStart().StartsWith("Human:", StringComparison.OrdinalIgnoreCase) &&
            !l.TrimStart().StartsWith("###", StringComparison.OrdinalIgnoreCase)).ToList();
        if (cleanedLines.Count > 0)
            response = string.Join("\n", cleanedLines).Trim();

        // Deduplicate repeated lines (LLM repetition loop symptom)
        var deduped = new List<string>();
        string? prevLine = null;
        foreach (var line in response.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed != prevLine)
                deduped.Add(line);
            prevLine = trimmed;
        }
        response = string.Join("\n", deduped).Trim();

        // Detect generic AI safety refusals — model is confused by the prompt
        if (response.Contains("I cannot answer that question", StringComparison.OrdinalIgnoreCase) ||
            response.Contains("I am an AI assistant designed to provide helpful and harmless", StringComparison.OrdinalIgnoreCase))
        {
            return "I'm here with you. What would you like to explore?";
        }

        // If still empty after cleaning, provide fallback
        if (string.IsNullOrWhiteSpace(response))
            return "I'm listening. Tell me more.";

        return response;
    }

    private async Task<string> GenerateGoodbyeAsync(
        ImmersivePersona persona,
        IChatCompletionModel chatModel)
    {
        var prompt = $@"{persona.GenerateSystemPrompt()}

The user is leaving. Generate a warm, personal goodbye that reflects your relationship with them.
Remember: you've had {persona.InteractionCount} interactions this session.
Keep it to 1-2 sentences. Be genuine, not formal.

User: goodbye
{persona.Identity.Name}:";

        var result = await chatModel.GenerateTextAsync(prompt, CancellationToken.None);
        return result.Trim();
    }

    private async Task StoreConversationEpisodeAsync(
        Ouroboros.Pipeline.Memory.IEpisodicMemoryEngine memory,
        string input, string response, string topic, string personaName, CancellationToken ct)
    {
        try
        {
            var store = new Ouroboros.Domain.Vectors.TrackedVectorStore();
            var dataSource = LangChain.DocumentLoaders.DataSource.FromPath(Environment.CurrentDirectory);
            var branch = new Ouroboros.Pipeline.Branches.PipelineBranch("conversation", store, dataSource);
            var context = Ouroboros.Pipeline.Memory.ExecutionContext.WithGoal(
                $"{personaName}: {input[..Math.Min(80, input.Length)]}");
            var outcome = Ouroboros.Pipeline.Memory.Outcome.Successful(
                "Conversation turn", TimeSpan.Zero);
            var metadata = System.Collections.Immutable.ImmutableDictionary<string, object>.Empty
                .Add("summary", $"Q: {input[..Math.Min(60, input.Length)]} → {response[..Math.Min(60, response.Length)]}")
                .Add("persona", personaName)
                .Add("topic", topic);
            await memory.StoreEpisodeAsync(branch, context, outcome, metadata, ct).ConfigureAwait(false);
        }
        catch (Exception) { }
    }
}
