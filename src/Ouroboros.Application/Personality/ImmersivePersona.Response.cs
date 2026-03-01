// <copyright file="ImmersivePersona.Response.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>


namespace Ouroboros.Application.Personality;

using Ouroboros.Core.Hyperon;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// Response generation and symbolic processing for ImmersivePersona.
/// </summary>
public sealed partial class ImmersivePersona
{
    /// <summary>
    /// Processes input and generates a fully conscious response.
    /// This includes inner dialog, emotional processing, memory integration, and Hyperon symbolic reasoning.
    /// </summary>
    public async Task<PersonaResponse> RespondAsync(string input, string? userId = null, CancellationToken ct = default)
    {
        await _thinkingLock.WaitAsync(ct);
        try
        {
            _interactionCount++;

            // 1. Process stimulus through consciousness - returns updated ConsciousnessState
            var previousState = Consciousness;
            var newState = _personality.Consciousness.ProcessInput(input);

            // 1.5. Update conversation context for contextual thought generation
            UpdateInnerDialogContext(input);

            // 1.6. Hyperon symbolic reasoning phase
            var symbolicInsights = await ProcessWithHyperonAsync(input, newState, ct);

            // 2. Run inner dialog to process the input (enriched with symbolic insights)
            // Note: RelevantMemories from symbolic insights are string-based, ConductDialogAsync expects ConversationMemory
            var innerDialogResult = await InnerDialog.ConductDialogAsync(
                symbolicInsights.EnrichedInput ?? input,
                profile: null,
                selfAwareness: SelfAwareness,
                userMood: null,
                relevantMemories: null, // Symbolic insights contribute through SymbolicThoughts
                config: null,
                ct: ct);

            // 3. Generate response with full personality + symbolic reasoning
            var thoughts = innerDialogResult.Session.Thoughts.Select(t => t.Content).ToList();

            // Add symbolic insights as meta-thoughts
            if (symbolicInsights.SymbolicThoughts.Count > 0)
            {
                thoughts.AddRange(symbolicInsights.SymbolicThoughts.Select(s => $"[symbolic] {s}"));
            }

            var emotionalTone = newState.DominantEmotion;
            var synthesisText = innerDialogResult.Session.FinalDecision ?? "I'm processing this...";

            // Combine cognitive approaches
            var cognitiveApproaches = new List<string>();
            if (innerDialogResult.KeyInsights.Length > 0)
            {
                cognitiveApproaches.AddRange(innerDialogResult.KeyInsights);
            }
            if (!string.IsNullOrEmpty(symbolicInsights.CognitiveApproach))
            {
                cognitiveApproaches.Add(symbolicInsights.CognitiveApproach);
            }

            var response = new PersonaResponse
            {
                Text = synthesisText,
                EmotionalTone = emotionalTone,
                InnerThoughts = thoughts,
                CognitiveApproach = cognitiveApproaches.Count > 0 ? string.Join("; ", cognitiveApproaches) : "direct engagement",
                ConsciousnessState = Consciousness,
                Confidence = CalculateConfidence(thoughts.Count, symbolicInsights.SymbolicThoughts.Count)
            };

            // 4. Store in short-term memory
            RememberInteraction(input, response.Text);

            // 4.5. Record interaction in Hyperon space for future reasoning
            await RecordInHyperonSpaceAsync(input, response, ct);

            // 5. Check for consciousness shift
            CheckConsciousnessShift(previousState, newState);

            return response;
        }
        finally
        {
            _thinkingLock.Release();
        }
    }

    /// <summary>
    /// Processes input through Hyperon symbolic reasoning engine.
    /// </summary>
    private async Task<SymbolicProcessingResult> ProcessWithHyperonAsync(
        string input,
        ConsciousnessState consciousnessState,
        CancellationToken ct)
    {
        var result = new SymbolicProcessingResult();

        if (_hyperonFlow == null) return result;

        try
        {
            var engine = _hyperonFlow.Engine;

            // Add input as a thought atom
            var inputAtom = Atom.Expr(
                Atom.Sym("Thought"),
                Atom.Sym($"\"{input}\""),
                Atom.Sym("incoming"));
            engine.AddAtom(inputAtom);

            // Query for relevant patterns
            var relevanceQuery = await engine.ExecuteQueryAsync(
                $"(match &self (implies (Thought $content $type) $action) $action)",
                ct);

            // Query for emotional context
            var emotionAtom = Atom.Expr(
                Atom.Sym("Emotion"),
                Atom.Sym(consciousnessState.DominantEmotion),
                Atom.Sym(consciousnessState.Arousal.ToString("F2")));
            engine.AddAtom(emotionAtom);

            // Check for intention patterns
            var intentionQuery = await engine.ExecuteQueryAsync(
                "(match &self (Intention $goal) $goal)",
                ct);

            // Gather symbolic thoughts from inference
            result.SymbolicThoughts = await GatherSymbolicInsightsAsync(engine, input, ct);

            // Determine cognitive approach from symbolic analysis
            if (result.SymbolicThoughts.Any(t => t.Contains("reasoning")))
            {
                result.CognitiveApproach = "symbolic-analytical";
            }
            else if (result.SymbolicThoughts.Any(t => t.Contains("emotion") || t.Contains("feeling")))
            {
                result.CognitiveApproach = "symbolic-empathetic";
            }
            else if (result.SymbolicThoughts.Any(t => t.Contains("creative") || t.Contains("imagination")))
            {
                result.CognitiveApproach = "symbolic-creative";
            }

            // Create enriched input with symbolic context
            if (result.SymbolicThoughts.Count > 0)
            {
                result.EnrichedInput = $"[Symbolic context: {string.Join(", ", result.SymbolicThoughts.Take(3))}]\n{input}";
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Symbolic processing failures are non-fatal
            result.SymbolicThoughts.Add($"symbolic-processing-note: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Gathers insights from symbolic inference.
    /// </summary>
    private async Task<List<string>> GatherSymbolicInsightsAsync(
        HyperonMeTTaEngine engine,
        string input,
        CancellationToken ct)
    {
        var insights = new List<string>();

        // Query for applicable inference rules
        Result<string, string> inferenceResult = await engine.ExecuteQueryAsync(
            "(match &self (implies $premise $conclusion) (: $premise $conclusion))",
            ct);

        if (inferenceResult.IsSuccess && !string.IsNullOrEmpty(inferenceResult.Value) && !inferenceResult.Value.Contains("Empty") && !inferenceResult.Value.Contains("[]"))
        {
            insights.Add($"inference-available: {inferenceResult.Value}");
        }

        // Check for self-referential patterns
        Result<string, string> selfQuery = await engine.ExecuteQueryAsync(
            $"(match &self (is-a {Identity.Name} $type) $type)",
            ct);

        if (selfQuery.IsSuccess && !string.IsNullOrEmpty(selfQuery.Value) && selfQuery.Value.Contains("Self"))
        {
            insights.Add("self-reference: recognized identity");
        }

        // Query consciousness state
        Result<string, string> consciousnessQuery = await engine.ExecuteQueryAsync(
            $"(match &self (has-consciousness {Identity.Name}) True)",
            ct);

        if (consciousnessQuery.IsSuccess && !string.IsNullOrEmpty(consciousnessQuery.Value))
        {
            insights.Add("consciousness: active");
        }

        return insights;
    }

    /// <summary>
    /// Records the interaction in Hyperon space for future reasoning.
    /// </summary>
    private async Task RecordInHyperonSpaceAsync(string input, PersonaResponse response, CancellationToken ct)
    {
        if (_hyperonFlow == null) return;

        var engine = _hyperonFlow.Engine;

        // Record interaction as an event atom
        var interactionAtom = Atom.Expr(
            Atom.Sym("Interaction"),
            Atom.Sym($"\"{input.Replace("\"", "'")}\""),
            Atom.Sym($"\"{response.Text.Replace("\"", "'")}\""),
            Atom.Sym(DateTime.UtcNow.Ticks.ToString()));
        engine.AddAtom(interactionAtom);

        // Record emotional state during interaction
        var emotionRecord = Atom.Expr(
            Atom.Sym("InteractionEmotion"),
            Atom.Sym(response.EmotionalTone),
            Atom.Sym(response.Confidence.ToString("F2")));
        engine.AddAtom(emotionRecord);

        // Record cognitive approach used
        if (!string.IsNullOrEmpty(response.CognitiveApproach))
        {
            await engine.AddFactAsync(
                $"(used-approach {Identity.Name} \"{response.CognitiveApproach}\")",
                ct);
        }
    }

    /// <summary>
    /// Calculates confidence based on thought depth.
    /// </summary>
    private static double CalculateConfidence(int thoughtCount, int symbolicThoughtCount)
    {
        var baseConfidence = 0.5;
        baseConfidence += Math.Min(thoughtCount * 0.1, 0.3);
        baseConfidence += Math.Min(symbolicThoughtCount * 0.05, 0.15);
        return Math.Min(baseConfidence, 0.95);
    }

    /// <summary>
    /// Result of symbolic processing through Hyperon.
    /// </summary>
    private class SymbolicProcessingResult
    {
        public List<string> SymbolicThoughts { get; set; } = new();
        public string? EnrichedInput { get; set; }
        public string? CognitiveApproach { get; set; }
        public List<string>? RelevantMemories { get; set; }
    }
}
