// <copyright file="PersonalityEngine.InnerDialog.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality;

using System.Text;

/// <summary>
/// Partial class containing inner dialog integration for context building and prompt assembly.
/// </summary>
public sealed partial class PersonalityEngine
{
    #region Inner Dialog Integration

    /// <summary>
    /// Conducts an inner dialog before generating a response.
    /// </summary>
    public async Task<InnerDialogResult> ConductInnerDialogAsync(
        string personaName,
        string userInput,
        InnerDialogConfig? config = null,
        CancellationToken ct = default)
    {
        // Get personality profile
        _profiles.TryGetValue(personaName, out var profile);

        // Detect user mood
        var userMood = DetectMoodFromInput(userInput);

        // Recall relevant memories if available
        List<ConversationMemory>? memories = null;
        if (HasMemory)
        {
            memories = await RecallConversationsAsync(userInput, personaName, 3, 0.5, ct);
        }

        // Conduct the inner dialog
        var result = await _innerDialogEngine.ConductDialogAsync(
            userInput,
            profile,
            _selfAwareness,
            userMood,
            memories,
            config,
            ct);

        return result;
    }

    /// <summary>
    /// Conducts a quick inner dialog for simple responses.
    /// </summary>
    public async Task<InnerDialogResult> QuickInnerDialogAsync(
        string personaName,
        string userInput,
        CancellationToken ct = default)
    {
        _profiles.TryGetValue(personaName, out var profile);
        return await _innerDialogEngine.QuickDialogAsync(userInput, profile, ct);
    }

    /// <summary>
    /// Gets the inner monologue text for the last dialog session.
    /// </summary>
    public string? GetLastInnerMonologue(string personaName)
    {
        var session = _innerDialogEngine.GetLastSession(personaName);
        return session?.GetMonologue();
    }

    /// <summary>
    /// Builds a prompt prefix based on inner dialog results.
    /// </summary>
    public static string BuildInnerDialogPromptPrefix(InnerDialogResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[INTERNAL REASONING CONTEXT]");

        // Add key insights
        if (result.KeyInsights.Length > 0)
        {
            sb.AppendLine("Key considerations:");
            foreach (var insight in result.KeyInsights.Take(3))
            {
                sb.AppendLine($"- {insight}");
            }
        }

        // Add response guidance
        if (result.ResponseGuidance.TryGetValue("tone", out var tone))
        {
            sb.AppendLine($"Suggested tone: {tone}");
        }

        if (result.ResponseGuidance.TryGetValue("acknowledge_feelings", out var ack) && (bool)ack)
        {
            sb.AppendLine("Note: User may be experiencing strong emotions - acknowledge appropriately.");
        }

        if (result.ResponseGuidance.TryGetValue("be_concise", out var concise) && (bool)concise)
        {
            sb.AppendLine("Note: Keep response focused and concise.");
        }

        if (result.ResponseGuidance.TryGetValue("include_creative", out var creative) && (bool)creative)
        {
            sb.AppendLine("Note: Consider including creative or unexpected elements.");
        }

        sb.AppendLine();
        return sb.ToString();
    }

    /// <summary>
    /// Generates a thinking trace for debugging or transparency.
    /// </summary>
    public static string GenerateThinkingTrace(InnerDialogResult result, bool verbose = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════");
        sb.AppendLine("           AI THINKING PROCESS             ");
        sb.AppendLine("═══════════════════════════════════════════");
        sb.AppendLine();

        if (verbose)
        {
            sb.Append(result.Session.GetMonologue());
        }
        else
        {
            // Summarized version
            sb.AppendLine($"\ud83d\udcdd Input: \"{TruncateForTrace(result.Session.UserInput, 50)}\"");
            sb.AppendLine($"\ud83c\udfaf Topic: {result.Session.Topic ?? "general"}");
            sb.AppendLine();

            // Key thoughts by type
            var thoughtsByType = result.Session.Thoughts
                .GroupBy(t => t.Type)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var (type, thoughts) in thoughtsByType)
            {
                var icon = type switch
                {
                    InnerThoughtType.Observation => "\ud83d\udc41\ufe0f",
                    InnerThoughtType.Emotional => "\ud83d\udcad",
                    InnerThoughtType.Analytical => "\ud83d\udd0d",
                    InnerThoughtType.SelfReflection => "\ud83e\ude9e",
                    InnerThoughtType.MemoryRecall => "\ud83d\udcda",
                    InnerThoughtType.Strategic => "\ud83c\udfaf",
                    InnerThoughtType.Ethical => "\u2696\ufe0f",
                    InnerThoughtType.Creative => "\ud83d\udca1",
                    InnerThoughtType.Synthesis => "\ud83d\udd17",
                    InnerThoughtType.Decision => "\u2705",
                    _ => "\ufffd"
                };

                sb.AppendLine($"{icon} {type}: {TruncateForTrace(thoughts.First().Content, 60)}");
            }

            sb.AppendLine();
            sb.AppendLine($"\ud83d\udcca Confidence: {result.Session.OverallConfidence:P0}");
            sb.AppendLine($"\u23f1\ufe0f Processing: {result.Session.ProcessingTime.TotalMilliseconds:F0}ms");
        }

        sb.AppendLine();
        sb.AppendLine($"\ud83d\udcac Suggested Tone: {result.SuggestedResponseTone}");

        if (result.ProactiveQuestion != null)
        {
            sb.AppendLine($"\u2753 Follow-up: {result.ProactiveQuestion}");
        }

        sb.AppendLine("═══════════════════════════════════════════");
        return sb.ToString();
    }

    private static string TruncateForTrace(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text[..(maxLength - 3)] + "...";
    }

    /// <summary>
    /// Simulates an inner dialog step-by-step for interactive/streaming display.
    /// </summary>
    public async Task<InnerDialogResult> StreamInnerDialogAsync(
        string personaName,
        string userInput,
        Action<InnerThought> onThought,
        CancellationToken ct = default)
    {
        _profiles.TryGetValue(personaName, out var profile);
        var userMood = DetectMoodFromInput(userInput);

        // Start the dialog
        var result = await _innerDialogEngine.ConductDialogAsync(
            userInput,
            profile,
            _selfAwareness,
            userMood,
            null, // Skip memory for streaming
            InnerDialogConfig.Default,
            ct);

        // Stream thoughts to callback
        foreach (var thought in result.Session.Thoughts)
        {
            onThought(thought);
            await Task.Delay(50, ct); // Small delay for visual effect
        }

        return result;
    }

    /// <summary>
    /// Conducts an autonomous inner dialog session without external input.
    /// </summary>
    public async Task<InnerDialogResult> ConductAutonomousDialogAsync(
        string personaName,
        InnerDialogConfig? config = null,
        CancellationToken ct = default)
    {
        _profiles.TryGetValue(personaName, out var profile);
        return await _innerDialogEngine.ConductAutonomousDialogAsync(profile, _selfAwareness, config, ct);
    }

    /// <summary>
    /// Registers a custom thought provider for extensible thought generation.
    /// </summary>
    public void RegisterThoughtProvider(IThoughtProvider provider)
    {
        _innerDialogEngine.RegisterProvider(provider);
    }

    /// <summary>
    /// Removes a thought provider by name.
    /// </summary>
    public bool RemoveThoughtProvider(string name)
    {
        return _innerDialogEngine.RemoveProvider(name);
    }

    /// <summary>
    /// Gets a snapshot of the AI's current autonomous inner state.
    /// </summary>
    public AutonomousInnerState GetAutonomousInnerState(string personaName)
    {
        _profiles.TryGetValue(personaName, out var profile);

        var consciousness = GetCurrentConsciousnessState();
        var lastSession = _innerDialogEngine.GetLastSession(personaName);

        // Gather background thoughts from recent dialog sessions
        var recentSessions = _innerDialogEngine.GetSessionHistory(personaName, 5);
        var backgroundThoughts = recentSessions
            .SelectMany(s => s.Thoughts)
            .Where(t => t.IsAutonomous)
            .TakeLast(20)
            .ToList();

        return new AutonomousInnerState(
            PersonaName: personaName,
            Consciousness: consciousness,
            LastDialogSession: lastSession,
            BackgroundThoughts: backgroundThoughts,
            PendingAutonomousThoughts: [],
            CurrentMood: profile?.CurrentMood,
            ActiveTraits: profile?.GetActiveTraits(3).Select(t => t.Name!).ToArray() ?? Array.Empty<string>(),
            Timestamp: DateTime.UtcNow);
    }

    /// <summary>
    /// Generates a human-readable narrative of the AI's current inner state.
    /// </summary>
    public string GenerateInnerStateNarrative(string personaName)
    {
        AutonomousInnerState state = GetAutonomousInnerState(personaName);
        StringBuilder sb = new();

        sb.AppendLine("\u2554\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2557");
        sb.AppendLine("\u2551        AUTONOMOUS INNER STATE             \u2551");
        sb.AppendLine("\u255a\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u255d");
        sb.AppendLine();

        // Consciousness layer
        sb.AppendLine("\ud83e\udde0 CONSCIOUSNESS:");
        sb.AppendLine($"   Arousal: {state.Consciousness.Arousal:P0} ({state.Consciousness.DominantEmotion})");
        if (!string.IsNullOrEmpty(state.Consciousness.CurrentFocus))
            sb.AppendLine($"   Focus: {state.Consciousness.CurrentFocus}");
        sb.AppendLine();

        // Active traits
        if (state.ActiveTraits.Length > 0)
        {
            sb.AppendLine("\ud83c\udfad ACTIVE TRAITS:");
            foreach (string trait in state.ActiveTraits)
            {
                sb.AppendLine($"   \ufffd {trait}");
            }
            sb.AppendLine();
        }

        // Background thoughts
        if (state.BackgroundThoughts.Count > 0)
        {
            sb.AppendLine("\ud83d\udcad BACKGROUND THOUGHTS:");
            foreach (var thought in state.BackgroundThoughts.TakeLast(3))
            {
                var icon = thought.IsAutonomous ? "\ud83c\udf00" : "\ud83d\udcac";
                sb.AppendLine($"   {icon} [{thought.Type}] {TruncateForTrace(thought.Content, 50)}");
            }
            sb.AppendLine();
        }

        // Pending autonomous thoughts
        if (state.PendingAutonomousThoughts.Count > 0)
        {
            sb.AppendLine("\ud83d\udd2e PENDING AUTONOMOUS THOUGHTS:");
            foreach (var thought in state.PendingAutonomousThoughts)
            {
                sb.AppendLine($"   \u2192 [{thought.Type}] {TruncateForTrace(thought.Content, 50)}");
            }
            sb.AppendLine();
        }

        // Last session summary
        if (state.LastDialogSession != null)
        {
            sb.AppendLine("\ud83d\udcdd LAST DIALOG:");
            sb.AppendLine($"   Topic: {state.LastDialogSession.Topic ?? "general"}");
            sb.AppendLine($"   Thoughts: {state.LastDialogSession.Thoughts.Count}");
            sb.AppendLine($"   Confidence: {state.LastDialogSession.OverallConfidence:P0}");
        }

        sb.AppendLine();
        sb.AppendLine($"\u23f1\ufe0f Snapshot taken at {state.Timestamp:HH:mm:ss}");

        return sb.ToString();
    }

    #endregion
}
