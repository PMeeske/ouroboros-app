// <copyright file="PersonalityEngine.Context.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality;

using System.Text;

/// <summary>
/// Partial class containing consciousness integration (Pavlovian layer)
/// for context building and prompt assembly.
/// </summary>
public sealed partial class PersonalityEngine
{
    #region Consciousness Integration (Pavlovian Layer)

    /// <summary>
    /// Processes a stimulus through the consciousness layer, triggering conditioned responses.
    /// </summary>
    public Task<ConsciousnessState> ProcessConsciousStimulusAsync(
        string stimulusType,
        string stimulusContent,
        double intensity = 0.7,
        CancellationToken ct = default)
    {
        _ = intensity;
        _ = ct;

        ConsciousnessState state = _consciousness.ProcessInput(stimulusContent, stimulusType);
        return Task.FromResult(state);
    }

    /// <summary>
    /// Gets the current consciousness state including arousal, attention, and active responses.
    /// </summary>
    public ConsciousnessState GetCurrentConsciousnessState()
    {
        return _consciousness.CurrentState;
    }

    /// <summary>
    /// Creates a new conditioned association through experience.
    /// </summary>
    public void ConditionNewAssociation(
        string neutralStimulusType,
        string responseType,
        double reinforcementStrength = 0.5)
    {
        _consciousness.AddConditionedAssociation(
            neutralStimulusType,
            responseType,
            reinforcementStrength);
    }

    /// <summary>
    /// Reinforces an existing conditioned association (strengthens the bond).
    /// </summary>
    public void ReinforceAssociation(
        string stimulusType,
        string responseType,
        double reinforcementAmount = 0.1)
    {
        _consciousness.Reinforce(stimulusType, responseType, reinforcementAmount);
    }

    /// <summary>
    /// Weakens an existing conditioned association (extinction).
    /// </summary>
    public void ExtinguishAssociation(
        string stimulusType,
        string responseType,
        double extinctionAmount = 0.05)
    {
        _consciousness.Extinguish(stimulusType, responseType, extinctionAmount);
    }

    /// <summary>
    /// Gets all currently active conditioned responses above threshold.
    /// </summary>
    public IReadOnlyDictionary<string, double> GetActiveConditionedResponses(double threshold = 0.3)
    {
        return _consciousness.GetActiveResponses(threshold);
    }

    /// <summary>
    /// Generates a conscious experience narrative from the current state.
    /// </summary>
    public string GenerateConsciousnessNarrative()
    {
        ConsciousnessState state = _consciousness.CurrentState;
        StringBuilder sb = new();

        sb.AppendLine("[CONSCIOUSNESS STREAM]");
        sb.AppendLine();

        // Arousal description
        string arousalDesc = state.Arousal switch
        {
            < 0.2 => "deeply calm and contemplative",
            < 0.4 => "relaxed yet attentive",
            < 0.6 => "moderately engaged",
            < 0.8 => "highly alert and responsive",
            _ => "intensely activated and focused"
        };
        sb.AppendLine($"Arousal State: {arousalDesc} ({state.Arousal:P0})");
        sb.AppendLine($"Dominant Emotion: {state.DominantEmotion} (Valence: {state.Valence:+0.00;-0.00})");

        // Attention description
        if (!string.IsNullOrEmpty(state.CurrentFocus))
        {
            sb.AppendLine($"Attention Focus: {state.CurrentFocus}");
            sb.AppendLine($"Awareness Level: {state.Awareness:P0}");
        }

        // Active conditioned responses
        IReadOnlyDictionary<string, double> activeResponses = _consciousness.GetActiveResponses(0.3);
        if (activeResponses.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Active Conditioned Responses:");
            foreach (KeyValuePair<string, double> kvp in activeResponses.OrderByDescending(kvp => kvp.Value).Take(3))
            {
                string bar = new string('#', (int)(kvp.Value * 10));
                string empty = new string('-', 10 - (int)(kvp.Value * 10));
                sb.AppendLine($"  * {kvp.Key}: [{bar}{empty}] {kvp.Value:P0}");
            }
        }

        // Attentional spotlight
        if (state.AttentionalSpotlight.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Attentional Spotlight:");
            foreach (string item in state.AttentionalSpotlight.Take(3))
            {
                sb.AppendLine($"  → {item}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Integrates consciousness processing with inner dialog for enhanced self-awareness.
    /// </summary>
    public async Task<(ConsciousnessState Consciousness, InnerDialogResult Dialog)> ProcessWithFullAwarenessAsync(
        string personaName,
        string userInput,
        CancellationToken ct = default)
    {
        // First, process through consciousness layer
        string stimulusType = ClassifyStimulusType(userInput);
        ConsciousnessState consciousnessState = await ProcessConsciousStimulusAsync(
            stimulusType,
            userInput,
            0.7,
            ct);

        // Get active responses for decision making
        IReadOnlyDictionary<string, double> activeResponses = _consciousness.GetActiveResponses(0.3);

        // Create consciousness-aware config for inner dialog
        InnerDialogConfig config = new(
            EnableEmotionalProcessing: true,
            EnableMemoryRecall: true,
            EnableEthicalChecks: activeResponses.ContainsKey("caution") || activeResponses.ContainsKey("empathy"),
            EnableCreativeThinking: activeResponses.ContainsKey("excitement") || activeResponses.ContainsKey("interest"),
            MaxThoughts: 12,
            ProcessingIntensity: consciousnessState.Arousal,
            TopicHint: consciousnessState.CurrentFocus);

        // Then process through inner dialog with consciousness context
        InnerDialogResult dialogResult = await ConductInnerDialogAsync(
            personaName,
            userInput,
            config,
            ct);

        return (consciousnessState, dialogResult);
    }

    /// <summary>
    /// Classifies the type of stimulus from user input.
    /// </summary>
    private static string ClassifyStimulusType(string input)
    {
        var lowered = input.ToLowerInvariant();

        return lowered switch
        {
            var s when s.StartsWith("hello") || s.StartsWith("hi ") || s.StartsWith("hey") => "greeting",
            var s when s.Contains('?') => "question",
            var s when s.Contains("thank") || s.Contains("great") || s.Contains("awesome") => "praise",
            var s when s.Contains("wrong") || s.Contains("bad") || s.Contains("fix") => "criticism",
            var s when s.Contains("help") || s.Contains("please") => "help",
            var s when s.Contains("learn") || s.Contains("teach") || s.Contains("explain") => "learning",
            var s when s.Contains("error") || s.Contains("fail") || s.Contains("broken") => "error",
            var s when s.Contains("done") || s.Contains("worked") || s.Contains("success") => "success",
            _ => "neutral"
        };
    }

    /// <summary>
    /// Gets a summary of the consciousness system's learned associations.
    /// </summary>
    public string GetConditioningSummary()
    {
        return _consciousness.GetConditioningSummary();
    }

    /// <summary>
    /// Performs habituation - reduces response to repeated stimuli.
    /// </summary>
    public void ApplyHabituation(string stimulusType, double habituationRate = 0.1)
    {
        _consciousness.ApplyHabituation(stimulusType, habituationRate);
    }

    /// <summary>
    /// Performs sensitization - increases response to significant stimuli.
    /// </summary>
    public void ApplySensitization(string stimulusType, double sensitizationRate = 0.1)
    {
        _consciousness.ApplySensitization(stimulusType, sensitizationRate);
    }

    #endregion
}
