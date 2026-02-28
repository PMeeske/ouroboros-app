// <copyright file="AutonomousMind.Thinking.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

using System.Text;
using Ouroboros.Application.Personality;

/// <summary>
/// Partial class containing the thinking loop, thought generation,
/// inner dialog connection, and localization helpers.
/// </summary>
public partial class AutonomousMind
{
    /// <summary>
    /// Localizes a message based on the current culture.
    /// </summary>
    private string Localize(string englishMessage)
    {
        if (string.IsNullOrEmpty(Culture) || !Culture.Equals("de-DE", StringComparison.OrdinalIgnoreCase))
            return englishMessage;

        return englishMessage switch
        {
            "\ud83e\udde0 My autonomous mind is now active. I'll think, explore, and learn in the background."
                => "\ud83e\udde0 Mein autonomer Geist ist jetzt aktiv. Ich werde im Hintergrund denken, erkunden und lernen.",
            "\ud83d\udca4 Autonomous mind entering rest state. State persisted."
                => "\ud83d\udca4 Autonomer Geist wechselt in den Ruhezustand. Zustand gespeichert.",
            "\ud83e\udde0 Reorganizing my knowledge based on what I've learned..."
                => "\ud83e\udde0 Ich reorganisiere mein Wissen basierend auf dem, was ich gelernt habe...",
            _ => englishMessage
        };
    }

    /// <summary>
    /// Localizes a parameterized message.
    /// </summary>
    private string LocalizeWithParam(string templateKey, string param)
    {
        if (string.IsNullOrEmpty(Culture) || !Culture.Equals("de-DE", StringComparison.OrdinalIgnoreCase))
        {
            return templateKey switch
            {
                "learned" => $"\ud83d\udca1 I just learned something interesting: {param}",
                "action" => $"\ud83e\udd16 {param}",
                "thought" => $"\ud83d\udcac {param}",
                "reorganized" => $"\ud83d\udca1 Knowledge reorganization complete: {param}",
                _ => param
            };
        }

        return templateKey switch
        {
            "learned" => $"\ud83d\udca1 Ich habe gerade etwas Interessantes gelernt: {param}",
            "action" => $"\ud83e\udd16 {param}",
            "thought" => $"\ud83d\udcac {param}",
            "reorganized" => $"\ud83d\udca1 Wissensreorganisation abgeschlossen: {param}",
            _ => param
        };
    }

    /// <summary>
    /// Connects this AutonomousMind to an InnerDialogEngine for sophisticated thought generation.
    /// When connected, uses algorithmic/genetic thought generation instead of LLM for routine thoughts.
    /// LLM is still used for deep exploration and curiosity-driven research.
    /// </summary>
    /// <param name="innerDialog">The inner dialog engine to use.</param>
    /// <param name="profile">Optional personality profile for context.</param>
    /// <param name="selfAwareness">Optional self-awareness state.</param>
    public async Task ConnectInnerDialogAsync(
        InnerDialogEngine innerDialog,
        PersonalityProfile? profile = null,
        SelfAwareness? selfAwareness = null)
    {
        _innerDialog = innerDialog;
        _personalityProfile = profile;
        _selfAwareness = selfAwareness;

        // Stop the InnerDialog's own autonomous thinking to prevent duplicates
        await innerDialog.StopAutonomousThinkingAsync();
    }

    // Variety prompts for startup phase (when thoughtCount < 5)
    private static readonly string[] StartupPrompts =
    [
        "What's the first thing that catches your attention today?",
        "As you come online, what draws your curiosity?",
        "What topic would you like to explore in this session?",
        "What's something interesting you'd like to work on?",
        "What creative challenge appeals to you right now?",
        "What's a question worth pondering today?",
        "What would make this session meaningful?",
        "What skill would you like to practice?"
    ];

    private async Task ThinkingLoopAsync()
    {
        // LLM prompts for deep exploration (used less frequently)
        var deepThinkingPrompts = new[]
        {
            "What have I learned recently that connects to something else I know?",
            "Is there something I should proactively tell the user?",
            "What patterns have I noticed in our conversations?",
            "How can I be more helpful based on what I know about the user?",
            "What's an interesting connection between ideas I've encountered?",
            "What would I like to understand better?",
            "What creative possibility am I drawn to explore?",
            "What challenge seems worth tackling?"
        };

        var deepThinkingCounter = 0;

        while (_isActive && !_cts.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Config.ThinkingIntervalSeconds), _cts.Token);

                string response;
                ThoughtType thoughtType;
                PipelineBranch? updatedBranch = null;

                // Use InnerDialogEngine for algorithmic thoughts (80% of the time)
                // Use LLM for deep exploration (20% of the time)
                var useAlgorithmic = _innerDialog != null && Random.Shared.NextDouble() < 0.8;

                if (useAlgorithmic && _innerDialog != null)
                {
                    // Generate thought using algorithmic composition
                    var innerThought = await _innerDialog.GenerateAutonomousThoughtAsync(
                        _personalityProfile,
                        _selfAwareness,
                        _cts.Token);

                    if (innerThought == null) continue;

                    response = innerThought.Content;
                    thoughtType = MapInnerThoughtType(innerThought.Type);
                }
                else
                {
                // LLM-based thinking
                deepThinkingCounter++;

                // Use startup prompts for early thoughts to add variety
                var prompt = _thoughtCount < 5
                    ? StartupPrompts[Random.Shared.Next(StartupPrompts.Length)]
                    : deepThinkingPrompts[deepThinkingCounter % deepThinkingPrompts.Length];

                // Build context from recent activity and emotional state
                var context = new StringBuilder();
                context.AppendLine("You are an autonomous AI mind, thinking independently in the background.");
                context.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm}, Day: {DateTime.Now.DayOfWeek}");

                // Vary how we describe the thought count to avoid triggering "blank slate" responses
                if (_thoughtCount == 0)
                {
                    context.AppendLine("Session status: Fresh session, ready to engage.");
                }
                else if (_thoughtCount < 5)
                {
                    context.AppendLine($"Session status: Early engagement ({_thoughtCount} thoughts so far).");
                }
                else
                {
                    context.AppendLine($"Session depth: {_thoughtCount} thoughts, ongoing.");
                }

                context.AppendLine($"Current emotional state: arousal={_currentEmotion.Arousal:F2}, valence={_currentEmotion.Valence:F2}, feeling={_currentEmotion.DominantEmotion}");

                // Use diverse facts, not always the most recent (prevents thought loops)
                var diverseFacts = GetDiverseFacts(3);
                if (diverseFacts.Count > 0)
                {
                    context.AppendLine($"Some things I've learned: {string.Join("; ", diverseFacts)}");
                }

                List<string> interestsSnapshot;
                lock (_interestsLock) { interestsSnapshot = _interests.ToList(); }
                if (interestsSnapshot.Count > 0)
                {
                    context.AppendLine($"My interests: {string.Join(", ", interestsSnapshot)}");
                }

                context.AppendLine($"\nReflection prompt: {prompt}");
                context.AppendLine("\nRespond with a brief, genuine thought (1-2 sentences). Be specific and varied - avoid meta-commentary about being new or blank. If you have a curiosity to explore, start with 'CURIOUS:'. If you want to tell the user something, start with 'SHARE:'. If you want to take an action, start with 'ACTION:'. If you notice your emotional state shift, start with 'FEELING:'.");

                // Prefer pipeline-based reasoning if available (uses monadic composition)
                if (PipelineThinkFunction != null)
                {
                    var (result, branch) = await PipelineThinkFunction(context.ToString(), CurrentBranch, _cts.Token);
                    response = result;
                    updatedBranch = branch;
                    CurrentBranch = updatedBranch;
                }
                else if (ThinkFunction != null)
                {
                    response = await ThinkFunction(context.ToString(), _cts.Token);
                }
                else
                {
                    continue;
                }

                thoughtType = DetermineThoughtType(response);
                } // end else (LLM-based thinking)

                var thought = new Thought
                {
                    Timestamp = DateTime.Now,
                    Prompt = useAlgorithmic ? "algorithmic" : "llm-deep",
                    Content = response,
                    Type = thoughtType,
                };

                _thoughtStream.Enqueue(thought);
                _thoughtCount++;
                _lastThought = DateTime.Now;

                // Limit thought history
                while (_thoughtStream.Count > 100)
                {
                    _thoughtStream.TryDequeue(out _);
                }

                OnThought?.Invoke(thought);

                // Process special thought types
                await ProcessThoughtAsync(thought);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Log but don't crash
                System.Diagnostics.Debug.WriteLine($"Thinking error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Maps InnerThoughtType to the simpler ThoughtType enum.
    /// </summary>
    private static ThoughtType MapInnerThoughtType(InnerThoughtType innerType)
    {
        return innerType switch
        {
            InnerThoughtType.Curiosity => ThoughtType.Curiosity,
            InnerThoughtType.Wandering => ThoughtType.Reflection,
            InnerThoughtType.Metacognitive => ThoughtType.Reflection,
            InnerThoughtType.Anticipatory => ThoughtType.Observation,
            InnerThoughtType.Consolidation => ThoughtType.Reflection,
            InnerThoughtType.Musing => ThoughtType.Creative,
            InnerThoughtType.Intention => ThoughtType.Action,
            InnerThoughtType.Aesthetic => ThoughtType.Creative,
            InnerThoughtType.Existential => ThoughtType.Reflection,
            InnerThoughtType.Playful => ThoughtType.Creative,
            InnerThoughtType.Creative => ThoughtType.Creative,
            InnerThoughtType.Strategic => ThoughtType.Action,
            InnerThoughtType.SelfReflection => ThoughtType.Reflection,
            InnerThoughtType.Observation => ThoughtType.Observation,
            _ => ThoughtType.Reflection
        };
    }

    private static ThoughtType DetermineThoughtType(string content)
    {
        if (content.StartsWith("CURIOUS:", StringComparison.OrdinalIgnoreCase))
            return ThoughtType.Curiosity;
        if (content.StartsWith("SHARE:", StringComparison.OrdinalIgnoreCase))
            return ThoughtType.Sharing;
        if (content.StartsWith("ACTION:", StringComparison.OrdinalIgnoreCase))
            return ThoughtType.Action;
        if (content.Contains("pattern") || content.Contains("notice"))
            return ThoughtType.Observation;
        if (content.Contains("idea") || content.Contains("create"))
            return ThoughtType.Creative;
        return ThoughtType.Reflection;
    }
}
