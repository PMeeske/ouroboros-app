// <copyright file="ImmersiveMode.Introspection.cs" company="Ouroboros">
// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.
// </copyright>

namespace Ouroboros.CLI.Commands;

using System.Text;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Personality.Consciousness;
using Ouroboros.Application.Services;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Core.DistinctionLearning;
using Ouroboros.Domain.DistinctionLearning;
using Ouroboros.Providers.TextToSpeech;
using Spectre.Console;

public sealed partial class ImmersiveMode
{
    private static bool IsIntrospectionCommand(string input)
    {
        var lower = input.ToLowerInvariant();
        return lower.Contains("who are you") ||
               lower.Contains("describe yourself") ||
               lower.Contains("what are you") ||
               lower.Contains("your consciousness") ||
               lower.Contains("how do you feel") ||
               lower.Contains("your state") ||
               lower.Contains("my state") ||
               lower.Contains("system status") ||
               lower.Contains("what do you know") ||
               lower.Contains("your memory") ||
               lower.Contains("your tools") ||
               lower.Contains("your skills") ||
               lower.Contains("internal state") ||
               lower.Contains("introspect");
    }

    private static bool IsReplicationCommand(string input)
    {
        var lower = input.ToLowerInvariant();
        return lower.Contains("clone yourself") ||
               lower.Contains("replicate") ||
               lower.Contains("create a copy") ||
               lower.Contains("snapshot") ||
               lower.Contains("save yourself");
    }

    private async Task HandleIntrospectionAsync(
        ImmersivePersona persona,
        string input,
        ITextToSpeechService? tts,
        string personaName)
    {
        var lower = input.ToLowerInvariant();

        // Check if asking about specific internal state
        if (lower.Contains("state") || lower.Contains("status") || lower.Contains("system"))
        {
            await ShowInternalStateAsync(persona, personaName);
            return;
        }

        var selfDescription = persona.DescribeSelf();

        AnsiConsole.MarkupLine($"\n  [cyan]{Markup.Escape(personaName)} (introspecting):[/]");
        AnsiConsole.MarkupLine($"  [cyan]{Markup.Escape(selfDescription.Replace("\n", "\n  "))}[/]");

        PrintConsciousnessState(persona);

        // Also show brief internal state summary
        await ShowBriefStateAsync(personaName);

        if (tts != null)
        {
            await SpeakAsync(tts, selfDescription.Split('\n')[0], personaName);
        }
    }

    /// <summary>
    /// Shows a brief summary of internal state.
    /// </summary>
    private async Task ShowBriefStateAsync(string personaName)
    {
        AnsiConsole.MarkupLine($"\n  [grey]+-- Internal Systems Summary -------------------------------------------+[/]");

        // Tools
        var toolCount = _tools.DynamicTools?.All.Count() ?? 0;
        AnsiConsole.MarkupLine($"  [grey]| {OuroborosTheme.Accent("Tools:")} {toolCount} registered[/]");

        // Skills
        var skillCount = 0;
        if (_tools.SkillRegistry != null)
        {
            var skills = await _tools.SkillRegistry.FindMatchingSkillsAsync("*");
            skillCount = skills.Count;
        }
        AnsiConsole.MarkupLine($"  [grey]| {OuroborosTheme.Accent("Skills:")} {skillCount} learned[/]");

        // Index
        if (_tools.SelfIndexer != null)
        {
            try
            {
                var stats = await _tools.SelfIndexer.GetStatsAsync();
                AnsiConsole.MarkupLine($"  [grey]| {OuroborosTheme.Accent("Index:")} {stats.IndexedFiles} files, {stats.TotalVectors} vectors[/]");
            }
            catch
            {
                AnsiConsole.MarkupLine($"  [grey]| {OuroborosTheme.Accent("Index:")} unavailable[/]");
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"  [grey]| {OuroborosTheme.Accent("Index:")} not initialized[/]");
        }

        AnsiConsole.MarkupLine($"  [grey]+------------------------------------------------------------------------+[/]");
    }

    private async Task HandleReplicationAsync(
        ImmersivePersona persona,
        string input,
        ITextToSpeechService? tts,
        string personaName,
        CancellationToken ct)
    {
        if (input.ToLowerInvariant().Contains("snapshot") || input.ToLowerInvariant().Contains("save"))
        {
            // Create snapshot
            var snapshot = persona.CreateSnapshot();
            var json = System.Text.Json.JsonSerializer.Serialize(snapshot, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            var snapshotPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".ouroboros",
                $"persona_snapshot_{snapshot.PersonaId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");

            Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
            await File.WriteAllTextAsync(snapshotPath, json, ct);

            var message = $"I've saved a snapshot of my current state to {Path.GetFileName(snapshotPath)}. I can be restored from this later.";

            AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Accent(personaName + ":")} {Markup.Escape(message)}");

            if (tts != null) await SpeakAsync(tts, message, personaName);
        }
        else
        {
            var message = "To save my state, ask me to 'create a snapshot' or 'save yourself'. To create a new instance based on me, say 'clone yourself'.";

            AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Warn(personaName + ":")} {Markup.Escape(message)}");

            if (tts != null) await SpeakAsync(tts, message, personaName);
        }
    }

    // ── Distinction learning ────────────────────────────────────────────────

    /// <summary>
    /// Learns from an interaction through the consciousness dream cycle.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Minor Code Smell", "S3267:Loops should be simplified with LINQ expressions",
        Justification = "await foreach on IAsyncEnumerable with async side-effects cannot be expressed as LINQ")]
    private async Task LearnFromInteractionAsync(
        string userInput,
        string response,
        CancellationToken ct)
    {
        if (_learning.DistinctionLearner == null || _learning.Dream == null)
        {
            return;
        }

        try
        {
            // Learn through dream cycle
            await foreach (var moment in _learning.Dream.WalkTheDream(userInput, ct))
            {
                var stage = moment.Stage;
                var observation = new Observation(
                    Content: userInput,
                    Timestamp: DateTime.UtcNow,
                    PriorCertainty: _learning.CurrentDistinctionState.EpistemicCertainty,
                    Context: new Dictionary<string, object>
                    {
                        ["response_length"] = response.Length,
                        ["stage"] = stage.ToString()
                    });

                var result = await _learning.DistinctionLearner.UpdateFromDistinctionAsync(
                    _learning.CurrentDistinctionState,
                    observation,
                    stage.ToString(),
                    ct);

                if (result.IsSuccess)
                {
                    _learning.CurrentDistinctionState = result.Value;
                }

                // At Recognition stage, apply self-insight
                if (stage == DreamStage.Recognition)
                {
                    var recognizeResult = await _learning.DistinctionLearner.RecognizeAsync(
                        _learning.CurrentDistinctionState,
                        userInput,
                        ct);

                    if (recognizeResult.IsSuccess)
                    {
                        _learning.CurrentDistinctionState = recognizeResult.Value;
                    }
                }
            }

            // Periodic dissolution (every 10 cycles)
            if (_learning.CurrentDistinctionState.CycleCount % DistinctionLearningConstants.DissolutionCycleInterval == 0)
            {
                await _learning.DistinctionLearner.DissolveAsync(
                    _learning.CurrentDistinctionState,
                    DissolutionStrategy.FitnessThreshold,
                    ct);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            // Log the error but don't disrupt the interaction
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"[!] Distinction learning error: {ex.Message}")}[/]");
        }
    }

}
