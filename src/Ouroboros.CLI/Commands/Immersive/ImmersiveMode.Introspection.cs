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
    private bool IsIntrospectionCommand(string input)
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

    private bool IsReplicationCommand(string input)
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
        var toolCount = _dynamicTools?.All.Count() ?? 0;
        AnsiConsole.MarkupLine($"  [grey]| {OuroborosTheme.Accent("Tools:")} {toolCount} registered[/]");

        // Skills
        var skillCount = 0;
        if (_skillRegistry != null)
        {
            var skills = await _skillRegistry.FindMatchingSkillsAsync("*");
            skillCount = skills.Count;
        }
        AnsiConsole.MarkupLine($"  [grey]| {OuroborosTheme.Accent("Skills:")} {skillCount} learned[/]");

        // Index
        if (_selfIndexer != null)
        {
            try
            {
                var stats = await _selfIndexer.GetStatsAsync();
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

    /// <summary>
    /// Shows comprehensive internal state report.
    /// </summary>
    private async Task ShowInternalStateAsync(ImmersivePersona persona, string personaName)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(OuroborosTheme.ThemedRule("OUROBOROS INTERNAL STATE REPORT"));
        AnsiConsole.WriteLine();

        // 1. Consciousness State
        var consciousness = persona.Consciousness;
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.GoldText("CONSCIOUSNESS")}");
        var consciousnessTable = OuroborosTheme.ThemedTable("Property", "Value");
        consciousnessTable.AddRow(Markup.Escape("Emotion"), Markup.Escape(consciousness.DominantEmotion));
        consciousnessTable.AddRow(Markup.Escape("Valence"), Markup.Escape($"{consciousness.Valence:+0.00;-0.00}"));
        consciousnessTable.AddRow(Markup.Escape("Arousal"), Markup.Escape($"{consciousness.Arousal:P0}"));
        consciousnessTable.AddRow(Markup.Escape("Focus"), Markup.Escape(consciousness.CurrentFocus));
        consciousnessTable.AddRow(Markup.Escape("Active associations"), Markup.Escape($"{consciousness.ActiveAssociations?.Count ?? 0}"));
        consciousnessTable.AddRow(Markup.Escape("Awareness level"), Markup.Escape($"{consciousness.Awareness:P0}"));
        AnsiConsole.Write(consciousnessTable);

        // 2. Memory State
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.GoldText("MEMORY")}");
        var memoryTable = OuroborosTheme.ThemedTable("Property", "Value");
        memoryTable.AddRow(Markup.Escape("Interactions this session"), Markup.Escape($"{persona.InteractionCount}"));
        memoryTable.AddRow(Markup.Escape("Uptime"), Markup.Escape($"{persona.Uptime.TotalMinutes:F1} minutes"));
        if (_pipelineState?.VectorStore != null)
        {
            memoryTable.AddRow(Markup.Escape("Vector store"), Markup.Escape("active"));
        }
        AnsiConsole.Write(memoryTable);

        // 3. Tools State
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.GoldText("TOOLS")}");
        var tools = _dynamicTools?.All.ToList() ?? new List<Ouroboros.Tools.ITool>();
        var toolsTable = OuroborosTheme.ThemedTable("Tool", "Status");
        toolsTable.AddRow(Markup.Escape("Registered tools"), Markup.Escape($"{tools.Count}"));
        foreach (var tool in tools.Take(10))
        {
            toolsTable.AddRow($"  {Markup.Escape(tool.Name)}", "");
        }
        if (tools.Count > 10)
        {
            toolsTable.AddRow(Markup.Escape($"... and {tools.Count - 10} more"), "");
        }
        AnsiConsole.Write(toolsTable);

        // 4. Skills State
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.GoldText("SKILLS")}");
        var skillsTable = OuroborosTheme.ThemedTable("Skill", "Details");
        if (_skillRegistry != null)
        {
            var skills = await _skillRegistry.FindMatchingSkillsAsync("*");
            skillsTable.AddRow(Markup.Escape("Learned skills"), Markup.Escape($"{skills.Count}"));
            foreach (var skill in skills.Take(10))
            {
                skillsTable.AddRow($"  {Markup.Escape(skill.Name)}", Markup.Escape($"{skill.Description?.Substring(0, Math.Min(40, skill.Description?.Length ?? 0))}..."));
            }
            if (skills.Count > 10)
            {
                skillsTable.AddRow(Markup.Escape($"... and {skills.Count - 10} more"), "");
            }
        }
        else
        {
            skillsTable.AddRow(Markup.Escape("Skill registry"), Markup.Escape("not initialized"));
        }
        AnsiConsole.Write(skillsTable);

        // 5. Index State
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.GoldText("KNOWLEDGE INDEX")}");
        var indexTable = OuroborosTheme.ThemedTable("Property", "Value");
        if (_selfIndexer != null)
        {
            try
            {
                var stats = await _selfIndexer.GetStatsAsync();
                indexTable.AddRow(Markup.Escape("Collection"), Markup.Escape(stats.CollectionName));
                indexTable.AddRow(Markup.Escape("Indexed files"), Markup.Escape($"{stats.IndexedFiles}"));
                indexTable.AddRow(Markup.Escape("Total vectors"), Markup.Escape($"{stats.TotalVectors}"));
                indexTable.AddRow(Markup.Escape("Vector dimensions"), Markup.Escape($"{stats.VectorSize}"));
                indexTable.AddRow(Markup.Escape("File watcher"), Markup.Escape("active"));
            }
            catch (Exception ex)
            {
                indexTable.AddRow(Markup.Escape("Index status"), Markup.Escape($"error - {ex.Message}"));
            }
        }
        else
        {
            indexTable.AddRow(Markup.Escape("Self-indexer"), Markup.Escape("not initialized"));
        }
        AnsiConsole.Write(indexTable);

        // 6. Learning State
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.GoldText("LEARNING SYSTEMS")}");
        var learningTable = OuroborosTheme.ThemedTable("Property", "Value");
        if (_toolLearner != null)
        {
            var learnerStats = _toolLearner.GetStats();
            learningTable.AddRow(Markup.Escape("Tool patterns"), Markup.Escape($"{learnerStats.TotalPatterns}"));
            learningTable.AddRow(Markup.Escape("Avg success rate"), Markup.Escape($"{learnerStats.AvgSuccessRate:P0}"));
            learningTable.AddRow(Markup.Escape("Total usage"), Markup.Escape($"{learnerStats.TotalUsage}"));
        }
        else
        {
            learningTable.AddRow(Markup.Escape("Tool learner"), Markup.Escape("not initialized"));
        }
        if (_interconnectedLearner != null)
        {
            learningTable.AddRow(Markup.Escape("Interconnected learner"), Markup.Escape("active"));
        }
        if (_pipelineState?.MeTTaEngine != null)
        {
            learningTable.AddRow(Markup.Escape("MeTTa reasoning engine"), Markup.Escape("active"));
        }
        AnsiConsole.Write(learningTable);

        // 7. Pipeline State
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.GoldText("PIPELINE ENGINE")}");
        var pipelineTable = OuroborosTheme.ThemedTable("Property", "Value");
        if (_pipelineState != null)
        {
            pipelineTable.AddRow(Markup.Escape("Pipeline"), Markup.Escape("initialized"));
            pipelineTable.AddRow(Markup.Escape("Current topic"), Markup.Escape(string.IsNullOrEmpty(_pipelineState.Topic) ? "(none)" : _pipelineState.Topic));
            pipelineTable.AddRow(Markup.Escape("Last query"), Markup.Escape(string.IsNullOrEmpty(_pipelineState.Query) ? "(none)" : _pipelineState.Query.Substring(0, Math.Min(40, _pipelineState.Query.Length)) + "..."));
        }
        else
        {
            pipelineTable.AddRow(Markup.Escape("Pipeline"), Markup.Escape("not initialized"));
        }
        var tokenCount = _allTokens?.Count ?? 0;
        pipelineTable.AddRow(Markup.Escape("Available tokens"), Markup.Escape($"{tokenCount}"));
        AnsiConsole.Write(pipelineTable);

        AnsiConsole.WriteLine();
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
    private async Task LearnFromInteractionAsync(
        string userInput,
        string response,
        CancellationToken ct)
    {
        if (_distinctionLearner == null || _dream == null)
        {
            return;
        }

        try
        {
            // Learn through dream cycle
            await foreach (var moment in _dream.WalkTheDream(userInput, ct))
            {
                var observation = new Observation(
                    Content: userInput,
                    Timestamp: DateTime.UtcNow,
                    PriorCertainty: _currentDistinctionState.EpistemicCertainty,
                    Context: new Dictionary<string, object>
                    {
                        ["response_length"] = response.Length,
                        ["stage"] = moment.Stage.ToString()
                    });

                var result = await _distinctionLearner.UpdateFromDistinctionAsync(
                    _currentDistinctionState,
                    observation,
                    moment.Stage.ToString(),
                    ct);

                if (result.IsSuccess)
                {
                    _currentDistinctionState = result.Value;
                }

                // At Recognition stage, apply self-insight
                if (moment.Stage == DreamStage.Recognition)
                {
                    var recognizeResult = await _distinctionLearner.RecognizeAsync(
                        _currentDistinctionState,
                        userInput,
                        ct);

                    if (recognizeResult.IsSuccess)
                    {
                        _currentDistinctionState = recognizeResult.Value;
                    }
                }
            }

            // Periodic dissolution (every 10 cycles)
            if (_currentDistinctionState.CycleCount % DistinctionLearningConstants.DissolutionCycleInterval == 0)
            {
                await _distinctionLearner.DissolveAsync(
                    _currentDistinctionState,
                    DissolutionStrategy.FitnessThreshold,
                    ct);
            }
        }
        catch (Exception ex)
        {
            // Log the error but don't disrupt the interaction
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"[!] Distinction learning error: {ex.Message}")}[/]");
        }
    }

    // ── Memory action handlers ──────────────────────────────────────────────

    private async Task<string> HandleMemoryRecallAsync(string topic, string personaName, CancellationToken ct)
    {
        if (_conversationMemory == null)
        {
            return "Conversation memory is not initialized.";
        }

        AnsiConsole.MarkupLine($"\n  [rgb(148,103,189)]\\[~] Searching memories for: {Markup.Escape(topic)}...[/]");

        try
        {
            var recall = await _conversationMemory.RecallAboutAsync(topic, ct);
            return recall;
        }
        catch (Exception ex)
        {
            return $"Memory search failed: {ex.Message}";
        }
    }

    private Task<string> HandleMemoryStatsAsync(string personaName, CancellationToken ct)
    {
        if (_conversationMemory == null)
        {
            return Task.FromResult("Conversation memory is not initialized.");
        }

        var stats = _conversationMemory.GetStats();
        var sb = new StringBuilder();
        sb.AppendLine("📝 **Conversation Memory Statistics**\n");
        sb.AppendLine($"  Total sessions: {stats.TotalSessions}");
        sb.AppendLine($"  Total conversation turns: {stats.TotalTurns}");
        sb.AppendLine($"  Current session turns: {stats.CurrentSessionTurns}");

        if (stats.OldestMemory.HasValue)
        {
            sb.AppendLine($"  Oldest memory: {stats.OldestMemory.Value:g}");
        }

        if (stats.CurrentSessionStart.HasValue)
        {
            sb.AppendLine($"  Current session started: {stats.CurrentSessionStart.Value:g}");
        }

        // Show recent sessions summary
        if (_conversationMemory.RecentSessions.Count > 0)
        {
            sb.AppendLine("\n  Recent sessions:");
            foreach (var session in _conversationMemory.RecentSessions.TakeLast(3))
            {
                sb.AppendLine($"    • {session.StartedAt:g}: {session.Turns.Count} turns");
            }
        }

        return Task.FromResult(sb.ToString());
    }

    // ── Autonomous mind action handlers ─────────────────────────────────────

    private string HandleMindState()
    {
        if (_autonomousMind == null)
        {
            return "Autonomous mind is not initialized.";
        }

        return _autonomousMind.GetMindState();
    }

    private string HandleShowInterests()
    {
        if (_autonomousMind == null)
        {
            return "Autonomous mind is not initialized.";
        }

        var facts = _autonomousMind.LearnedFacts;
        var sb = new StringBuilder();
        sb.AppendLine("🎯 **My Current Interests & Discoveries**\n");

        if (facts.Count == 0)
        {
            sb.AppendLine("I haven't discovered anything yet. Let me explore the internet!");
            sb.AppendLine("\n💡 Try: `think about AI` or `add interest quantum computing`");
        }
        else
        {
            sb.AppendLine("**Recent Discoveries:**");
            foreach (var fact in facts.TakeLast(10))
            {
                sb.AppendLine($"  💡 {fact}");
            }
        }

        return sb.ToString();
    }

    // ── Index action handlers ───────────────────────────────────────────────

    private async Task<string> HandleFullReindexAsync(string personaName, CancellationToken ct)
    {
        if (_selfIndexer == null)
        {
            return "Self-indexer is not available. Qdrant may not be connected.";
        }

        AnsiConsole.MarkupLine($"\n  [rgb(148,103,189)]\\[~] Starting full workspace reindex...[/]");

        var progress = new Progress<IndexingProgress>(p =>
        {
            if (p.ProcessedFiles % 10 == 0 && p.ProcessedFiles > 0)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Dim($"      [{p.ProcessedFiles}/{p.TotalFiles}] {Markup.Escape(p.CurrentFile ?? "")}"));
            }
        });

        try
        {
            var result = await _selfIndexer.FullReindexAsync(clearExisting: true, progress, ct);
            return $"Full reindex complete! Processed {result.ProcessedFiles} files, indexed {result.IndexedChunks} chunks in {result.Elapsed.TotalSeconds:F1}s. ({result.SkippedFiles} skipped, {result.ErrorFiles} errors)";
        }
        catch (Exception ex)
        {
            return $"Reindex failed: {ex.Message}";
        }
    }

    private async Task<string> HandleIncrementalReindexAsync(string personaName, CancellationToken ct)
    {
        if (_selfIndexer == null)
        {
            return "Self-indexer is not available. Qdrant may not be connected.";
        }

        AnsiConsole.MarkupLine($"\n  [rgb(148,103,189)]\\[~] Starting incremental reindex (changed files only)...[/]");

        var progress = new Progress<IndexingProgress>(p =>
        {
            if (!string.IsNullOrEmpty(p.CurrentFile))
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Dim($"      [{p.ProcessedFiles}/{p.TotalFiles}] {Markup.Escape(Path.GetFileName(p.CurrentFile) ?? "")}"));
            }
        });

        try
        {
            var result = await _selfIndexer.IncrementalIndexAsync(progress, ct);
            if (result.TotalFiles == 0)
            {
                return "No files have changed since last index. Workspace is up to date!";
            }
            return $"Incremental reindex complete! Updated {result.ProcessedFiles} files, indexed {result.IndexedChunks} chunks in {result.Elapsed.TotalSeconds:F1}s.";
        }
        catch (Exception ex)
        {
            return $"Incremental reindex failed: {ex.Message}";
        }
    }

    private async Task<string> HandleIndexSearchAsync(string query, string personaName, CancellationToken ct)
    {
        if (_selfIndexer == null)
        {
            return "Self-indexer is not available. Qdrant may not be connected.";
        }

        AnsiConsole.MarkupLine($"\n  [rgb(148,103,189)]\\[~] Searching indexed workspace for: {Markup.Escape(query)}[/]");

        try
        {
            var results = await _selfIndexer.SearchAsync(query, limit: 5, scoreThreshold: 0.3f, ct);

            if (results.Count == 0)
            {
                return "No matching content found in the indexed workspace.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Found {results.Count} relevant matches:\n");

            foreach (var result in results)
            {
                var relPath = Path.GetRelativePath(Environment.CurrentDirectory, result.FilePath);
                sb.AppendLine($"📄 **{relPath}** (chunk {result.ChunkIndex + 1}, score: {result.Score:F2})");
                sb.AppendLine($"   {result.Content.Substring(0, Math.Min(200, result.Content.Length))}...\n");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Index search failed: {ex.Message}";
        }
    }

    private async Task<string> HandleIndexStatsAsync(string personaName, CancellationToken ct)
    {
        if (_selfIndexer == null)
        {
            return "Self-indexer is not available. Qdrant may not be connected.";
        }

        try
        {
            var stats = await _selfIndexer.GetStatsAsync(ct);
            return $"📊 **Index Statistics**\n" +
                   $"  Collection: {stats.CollectionName}\n" +
                   $"  Indexed files: {stats.IndexedFiles}\n" +
                   $"  Total vectors: {stats.TotalVectors}\n" +
                   $"  Vector dimensions: {stats.VectorSize}";
        }
        catch (Exception ex)
        {
            return $"Failed to get index stats: {ex.Message}";
        }
    }

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
        catch (Exception ex)
        {
            // Don't fail the interaction just because learning persistence failed
            AnsiConsole.MarkupLine($"  [yellow]\\[WARN] Failed to record learnings: {Markup.Escape(ex.Message)}[/]");
        }
    }
}
