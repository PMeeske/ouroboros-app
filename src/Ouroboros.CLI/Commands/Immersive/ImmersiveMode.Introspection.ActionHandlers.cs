// <copyright file="ImmersiveMode.Introspection.ActionHandlers.cs" company="Ouroboros">
// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.
// </copyright>

namespace Ouroboros.CLI.Commands;

using System.Net.Http;
using System.Text;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Services;
using Ouroboros.CLI.Infrastructure;
using Spectre.Console;

public sealed partial class ImmersiveMode
{
    // ── State report handlers ─────────────────────────────────────────────

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
        var tools = _tools.DynamicTools?.All.ToList() ?? new List<Ouroboros.Tools.ITool>();
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
        if (_tools.SkillRegistry != null)
        {
            var skills = await _tools.SkillRegistry.FindMatchingSkillsAsync("*");
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
        if (_tools.SelfIndexer != null)
        {
            try
            {
                var stats = await _tools.SelfIndexer.GetStatsAsync();
                indexTable.AddRow(Markup.Escape("Collection"), Markup.Escape(stats.CollectionName));
                indexTable.AddRow(Markup.Escape("Indexed files"), Markup.Escape($"{stats.IndexedFiles}"));
                indexTable.AddRow(Markup.Escape("Total vectors"), Markup.Escape($"{stats.TotalVectors}"));
                indexTable.AddRow(Markup.Escape("Vector dimensions"), Markup.Escape($"{stats.VectorSize}"));
                indexTable.AddRow(Markup.Escape("File watcher"), Markup.Escape("active"));
            }
            catch (HttpRequestException ex)
            {
                indexTable.AddRow(Markup.Escape("Index status"), Markup.Escape($"error - {ex.Message}"));
            }
            catch (InvalidOperationException ex)
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
        if (_tools.ToolLearner != null)
        {
            var learnerStats = _tools.ToolLearner.GetStats();
            learningTable.AddRow(Markup.Escape("Tool patterns"), Markup.Escape($"{learnerStats.TotalPatterns}"));
            learningTable.AddRow(Markup.Escape("Avg success rate"), Markup.Escape($"{learnerStats.AvgSuccessRate:P0}"));
            learningTable.AddRow(Markup.Escape("Total usage"), Markup.Escape($"{learnerStats.TotalUsage}"));
        }
        else
        {
            learningTable.AddRow(Markup.Escape("Tool learner"), Markup.Escape("not initialized"));
        }
        if (_tools.InterconnectedLearner != null)
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

    // ── Memory action handlers ──────────────────────────────────────────────

    private async Task<string> HandleMemoryRecallAsync(string topic, string personaName, CancellationToken ct)
    {
        if (_tools.ConversationMemory == null)
        {
            return "Conversation memory is not initialized.";
        }

        AnsiConsole.MarkupLine($"\n  [rgb(148,103,189)]\\[~] Searching memories for: {Markup.Escape(topic)}...[/]");

        try
        {
            var recall = await _tools.ConversationMemory.RecallAboutAsync(topic, ct);
            return recall;
        }
        catch (HttpRequestException ex)
        {
            return $"Memory search failed: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            return $"Memory search failed: {ex.Message}";
        }
    }

    private Task<string> HandleMemoryStatsAsync(string personaName, CancellationToken ct)
    {
        if (_tools.ConversationMemory == null)
        {
            return Task.FromResult("Conversation memory is not initialized.");
        }

        var stats = _tools.ConversationMemory.GetStats();
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
        if (_tools.ConversationMemory.RecentSessions.Count > 0)
        {
            sb.AppendLine("\n  Recent sessions:");
            foreach (var session in _tools.ConversationMemory.RecentSessions.TakeLast(3))
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
        if (_tools.SelfIndexer == null)
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
            var result = await _tools.SelfIndexer.FullReindexAsync(clearExisting: true, progress, ct);
            return $"Full reindex complete! Processed {result.ProcessedFiles} files, indexed {result.IndexedChunks} chunks in {result.Elapsed.TotalSeconds:F1}s. ({result.SkippedFiles} skipped, {result.ErrorFiles} errors)";
        }
        catch (HttpRequestException ex)
        {
            return $"Reindex failed: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            return $"Reindex failed: {ex.Message}";
        }
    }

    private async Task<string> HandleIncrementalReindexAsync(string personaName, CancellationToken ct)
    {
        if (_tools.SelfIndexer == null)
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
            var result = await _tools.SelfIndexer.IncrementalIndexAsync(progress, ct);
            if (result.TotalFiles == 0)
            {
                return "No files have changed since last index. Workspace is up to date!";
            }
            return $"Incremental reindex complete! Updated {result.ProcessedFiles} files, indexed {result.IndexedChunks} chunks in {result.Elapsed.TotalSeconds:F1}s.";
        }
        catch (HttpRequestException ex)
        {
            return $"Incremental reindex failed: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            return $"Incremental reindex failed: {ex.Message}";
        }
    }

    private async Task<string> HandleIndexSearchAsync(string query, string personaName, CancellationToken ct)
    {
        if (_tools.SelfIndexer == null)
        {
            return "Self-indexer is not available. Qdrant may not be connected.";
        }

        AnsiConsole.MarkupLine($"\n  [rgb(148,103,189)]\\[~] Searching indexed workspace for: {Markup.Escape(query)}[/]");

        try
        {
            var results = await _tools.SelfIndexer.SearchAsync(query, limit: 5, scoreThreshold: 0.3f, ct);

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
        catch (HttpRequestException ex)
        {
            return $"Index search failed: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            return $"Index search failed: {ex.Message}";
        }
    }

    private async Task<string> HandleIndexStatsAsync(string personaName, CancellationToken ct)
    {
        if (_tools.SelfIndexer == null)
        {
            return "Self-indexer is not available. Qdrant may not be connected.";
        }

        try
        {
            var stats = await _tools.SelfIndexer.GetStatsAsync(ct);
            return $"📊 **Index Statistics**\n" +
                   $"  Collection: {stats.CollectionName}\n" +
                   $"  Indexed files: {stats.IndexedFiles}\n" +
                   $"  Total vectors: {stats.TotalVectors}\n" +
                   $"  Vector dimensions: {stats.VectorSize}";
        }
        catch (HttpRequestException ex)
        {
            return $"Failed to get index stats: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            return $"Failed to get index stats: {ex.Message}";
        }
    }
}
