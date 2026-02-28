// <copyright file="ImmersiveMode.Introspection.Learnings.cs" company="Ouroboros">
// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.
// </copyright>

namespace Ouroboros.CLI.Commands;

using Ouroboros.Application.Personality;
using Spectre.Console;

public sealed partial class ImmersiveMode
{
    /// <summary>
    /// Records learnings from each interaction to persistent storage.
    /// Captures insights, skills used, and knowledge gained during thinking.
    /// </summary>
    private async Task RecordInteractionLearningsAsync(
        string userInput,
        string response,
        ImmersivePersona persona,
        CancellationToken ct)
    {
        if (_networkStateProjector == null)
        {
            return;
        }

        try
        {
            var lowerInput = userInput.ToLowerInvariant();
            var lowerResponse = response.ToLowerInvariant();

            // Record skill usage
            if (_skillRegistry != null)
            {
                var matchedSkills = await _skillRegistry.FindMatchingSkillsAsync(userInput);
                foreach (var skill in matchedSkills.Take(3))
                {
                    await _networkStateProjector.RecordLearningAsync(
                        "skill_usage",
                        $"Used skill '{skill.Name}' for: {userInput.Substring(0, Math.Min(100, userInput.Length))}",
                        userInput,
                        confidence: 0.8,
                        ct: ct);
                }
            }

            // Record tool usage
            if (lowerResponse.Contains("tool") || lowerResponse.Contains("search") || lowerResponse.Contains("executed"))
            {
                await _networkStateProjector.RecordLearningAsync(
                    "tool_usage",
                    $"Tool interaction: {response.Substring(0, Math.Min(200, response.Length))}",
                    userInput,
                    confidence: 0.7,
                    ct: ct);
            }

            // Record learning/insight if response contains knowledge indicators
            if (lowerResponse.Contains("learned") || lowerResponse.Contains("discovered") ||
                lowerResponse.Contains("found out") || lowerResponse.Contains("interesting") ||
                lowerResponse.Contains("realized"))
            {
                await _networkStateProjector.RecordLearningAsync(
                    "insight",
                    response.Substring(0, Math.Min(300, response.Length)),
                    userInput,
                    confidence: 0.75,
                    ct: ct);
            }

            // Record emotional state changes
            var consciousnessState = persona.Consciousness;
            if (consciousnessState.Arousal > 0.6 || consciousnessState.Valence < -0.3)
            {
                await _networkStateProjector.RecordLearningAsync(
                    "emotional_context",
                    $"Emotional state during '{userInput.Substring(0, Math.Min(50, userInput.Length))}': arousal={consciousnessState.Arousal:F2}, valence={consciousnessState.Valence:F2}, emotion={consciousnessState.DominantEmotion}",
                    userInput,
                    confidence: 0.6,
                    ct: ct);
            }

            // Periodically save network state snapshot (every 10 interactions based on epoch)
            if (_networkStateProjector.CurrentEpoch % 10 == 0)
            {
                await _networkStateProjector.ProjectAndPersistAsync(
                    System.Collections.Immutable.ImmutableDictionary<string, string>.Empty
                        .Add("trigger", "periodic")
                        .Add("last_input", userInput.Substring(0, Math.Min(50, userInput.Length))),
                    ct);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            // Don't fail the interaction just because learning persistence failed
            AnsiConsole.MarkupLine($"  [yellow]\\[WARN] Failed to record learnings: {Markup.Escape(ex.Message)}[/]");
        }
    }
}
