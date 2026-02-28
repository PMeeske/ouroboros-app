// <copyright file="ImmersiveMode.Response.Generation.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.CLI.Commands;

using System.Text;
using Ouroboros.Application;
using Ouroboros.Application.Extensions;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Personality.Consciousness;
using Ouroboros.Application.Services;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Providers;
using Spectre.Console;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

public sealed partial class ImmersiveMode
{
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
        catch (HttpRequestException)
        {
            // Non-fatal — inner dialog failure does not block the response
        }

        // ── Episodic retrieval ────────────────────────────────────────────────
        string? episodicContext = null;
        if (_cognitive.EpisodicMemory != null)
        {
            try
            {
                var eps = await _cognitive.EpisodicMemory.RetrieveSimilarEpisodesAsync(
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
        _cognitive.Metacognition.StartTrace();
        _cognitive.Metacognition.AddStep(Ouroboros.Pipeline.Metacognition.ReasoningStepType.Observation,
            $"Input: {input[..Math.Min(80, input.Length)]}", "User query received");

        // ── Ethics gate ──────────────────────────────────────────────────────
        // Evaluate the user query before generating a response.
        // Void → refuse; Imaginary (requires approval) → add a caution note.
        string? ethicsCautionNote = null;
        if (_cognitive.Ethics != null)
        {
            try
            {
                var ethicsCheck = await _cognitive.Ethics.EvaluateActionAsync(
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
            catch (HttpRequestException) { /* Non-fatal — ethics check failure does not block response */ }
        }

        _cognitive.Metacognition.AddStep(Ouroboros.Pipeline.Metacognition.ReasoningStepType.Validation,
            ethicsCautionNote ?? "Ethics: clear", "MeTTa ethics evaluation");

        // ── CognitivePhysics shift ───────────────────────────────────────────
        // Compute context-shift cost when topic changes; surface as awareness in the prompt.
        string? cogPhysicsNote = null;
        if (_cognitive.CogPhysics != null)
        {
            try
            {
                var topic = Subsystems.ImmersiveSubsystem.ClassifyAvatarTopic(input);
                if (string.IsNullOrEmpty(topic)) topic = _cognitive.LastTopic;

                var shiftResult = await _cognitive.CogPhysics.ExecuteTrajectoryAsync(
                    _cognitive.CogState, [topic]).ConfigureAwait(false);

                if (shiftResult.IsSuccess)
                {
                    var prevTopic = _cognitive.LastTopic;
                    _cognitive.CogState = shiftResult.Value;
                    _cognitive.LastTopic = topic;

                    double resourcePct = _cognitive.CogState.Resources / 100.0;
                    if (prevTopic != topic && _cognitive.CogState.Compression > 0.3)
                        cogPhysicsNote = $"(Conceptual leap: {prevTopic} → {topic}, " +
                                         $"resources at {resourcePct:P0}, " +
                                         $"compression={_cognitive.CogState.Compression:F2})";
                }
            }
            catch (HttpRequestException) { /* Non-fatal */ }
        }

        _cognitive.Metacognition.AddStep(Ouroboros.Pipeline.Metacognition.ReasoningStepType.Inference,
            cogPhysicsNote ?? "No shift", "CognitivePhysics shift cost");

        // ── Neural-symbolic hybrid reasoning ─────────────────────────────────
        string? hybridNote = null;
        bool isComplexQuery = input.Contains('?') ||
            input.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 10;
        if (_cognitive.NeuralSymbolicBridge != null && isComplexQuery)
        {
            try
            {
                var hybrid = await _cognitive.NeuralSymbolicBridge.HybridReasonAsync(
                    input, Ouroboros.Agent.NeuralSymbolic.ReasoningMode.SymbolicFirst, ct)
                    .ConfigureAwait(false);
                if (hybrid.IsSuccess && !string.IsNullOrEmpty(hybrid.Value.Answer))
                    hybridNote = $"[Symbolic: {hybrid.Value.Answer[..Math.Min(120, hybrid.Value.Answer.Length)]}]";
            }
            catch (HttpRequestException) { }
            if (hybridNote != null)
                _cognitive.Metacognition.AddStep(Ouroboros.Pipeline.Metacognition.ReasoningStepType.Inference,
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
                var explanation = await _cognitive.CausalReasoning.ExplainCausallyAsync(
                    causalTerms.Value.Effect, [causalTerms.Value.Cause], graph, ct)
                    .ConfigureAwait(false);
                if (explanation.IsSuccess && !string.IsNullOrEmpty(explanation.Value.NarrativeExplanation))
                {
                    causalNote = $"[Causal: {explanation.Value.NarrativeExplanation[..Math.Min(150, explanation.Value.NarrativeExplanation.Length)]}]";
                    _cognitive.Metacognition.AddStep(Ouroboros.Pipeline.Metacognition.ReasoningStepType.Inference,
                        causalNote, "Causal reasoning engine");
                }
            }
            catch (HttpRequestException) { }
        }

        // ── Phi (IIT) annotation ─────────────────────────────────────────────
        // Measure conversational integration across recent turns.
        // High Phi → prefer orchestrated model; Low Phi → fall back to base model.
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
                var phiResult = _cognitive.PhiCalc.Compute(pathways);

                if (phiResult.Phi >= 0.5 && _learning.OrchestratedModel != null)
                    chatModel = _learning.OrchestratedModel; // Upgrade to collective model
                else if (phiResult.Phi < 0.2 && _learning.BaseModel != null)
                    chatModel = _learning.BaseModel;         // Keep single model for simple queries

                phiNote = $"Phi={phiResult.Phi:F2}";
            }
        }
        catch (InvalidOperationException) { /* Non-fatal — Phi computation edge case */ }

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

            var cleanContent = RecalledSessionPrefixRegex().Replace(content, string.Empty);

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
            _cognitive.Metacognition.AddStep(Ouroboros.Pipeline.Metacognition.ReasoningStepType.Conclusion,
                response[..Math.Min(80, response.Length)], "LLM response");
            var traceResult = _cognitive.Metacognition.EndTrace(response[..Math.Min(40, response.Length)], true);
            _cognitive.ResponseCount++;
            if (_cognitive.ResponseCount % 5 == 0 && traceResult.IsSuccess)
            {
                var reflection = _cognitive.Metacognition.ReflectOn(traceResult.Value);
                var metaMsg = $"  ✧ [[metacognition]] Q={reflection.QualityScore:F2} " +
                    $"| {(reflection.HasIssues ? Markup.Escape(reflection.Improvements.FirstOrDefault() ?? "–") : "Clean")}";
                AnsiConsole.MarkupLine($"\n[rgb(128,0,180)]{metaMsg}[/]");
            }

            if (_cognitive.EpisodicMemory != null)
                StoreConversationEpisodeAsync(_cognitive.EpisodicMemory, input, response,
                    _cognitive.LastTopic, personaName, CancellationToken.None)
                    .ObserveExceptions("ImmersiveMode.StoreConversationEpisode");

            return response;
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            AnsiConsole.MarkupLine($"  {IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned)} [red]{Markup.Escape($"[LLM error: {ex.Message}]")}[/]");
            return "I'm having trouble thinking right now. Let me try again.";
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"  {IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned)} [red]{Markup.Escape($"[LLM error: {ex.Message}]")}[/]");
            return "I'm having trouble thinking right now. Let me try again.";
        }
    }

    private static string CleanResponse(string raw, string personaName)
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
        response = MarkdownRoleMarkerRegex().Replace(response, "").Trim();

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

    private static async Task<string> GenerateGoodbyeAsync(
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

    private static async Task StoreConversationEpisodeAsync(
        Ouroboros.Pipeline.Memory.IEpisodicMemoryEngine memory,
        string input, string response, string topic, string personaName, CancellationToken ct)
    {
        try
        {
            var store = new Ouroboros.Domain.Vectors.TrackedVectorStore();
            var dataSource = LangChain.DocumentLoaders.DataSource.FromPath(Environment.CurrentDirectory);
            var branch = new Ouroboros.Pipeline.Branches.PipelineBranch("conversation", store, dataSource);
            var context = Ouroboros.Pipeline.Memory.PipelineExecutionContext.WithGoal(
                $"{personaName}: {input[..Math.Min(80, input.Length)]}");
            var outcome = Ouroboros.Pipeline.Memory.Outcome.Successful(
                "Conversation turn", TimeSpan.Zero);
            var metadata = System.Collections.Immutable.ImmutableDictionary<string, object>.Empty
                .Add("summary", $"Q: {input[..Math.Min(60, input.Length)]} → {response[..Math.Min(60, response.Length)]}")
                .Add("persona", personaName)
                .Add("topic", topic);
            await memory.StoreEpisodeAsync(branch, context, outcome, metadata, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException) { }
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^\[From [^\]]+\]:\s*")]
    private static partial System.Text.RegularExpressions.Regex RecalledSessionPrefixRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"###\s*(System|Human|Assistant)\s*", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex MarkdownRoleMarkerRegex();
}
