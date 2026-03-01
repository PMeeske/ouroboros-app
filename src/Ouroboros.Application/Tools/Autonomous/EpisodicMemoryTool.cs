// <copyright file="EpisodicMemoryTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Episodic memory tool that tags memories with experiential metadata.
/// Creates richer memory context than simple content recall.
/// </summary>
public class EpisodicMemoryTool : ITool
{
    private readonly IAutonomousToolContext _ctx;
    public EpisodicMemoryTool(IAutonomousToolContext context) => _ctx = context;
    public EpisodicMemoryTool() : this(AutonomousTools.DefaultContext) { }

    public string Name => "episodic_memory";
    public string Description => "Store or recall episodic memories with emotional/experiential context. Input: JSON {\"action\":\"store|recall|consolidate\", \"content\":\"...\", \"emotion\":\"...\", \"significance\":0-1}";
    public string? JsonSchema => null;

    private static readonly List<EpisodicMemoryEntry> _memories = [];
    private static readonly object _lock = new();

    /// <summary>
    /// Optional delegate to persist a memory to an external store (e.g. Qdrant EpisodicMemoryEngine).
    /// Delegates to <see cref="IAutonomousToolContext.EpisodicExternalStoreFunc"/>.
    /// </summary>
    public static Func<string, string, double, CancellationToken, Task>? ExternalStoreFunc
    {
        get => AutonomousTools.DefaultContext.EpisodicExternalStoreFunc;
        set => AutonomousTools.DefaultContext.EpisodicExternalStoreFunc = value;
    }

    /// <summary>
    /// Optional delegate to recall memories from an external persistent store (cross-session).
    /// Delegates to <see cref="IAutonomousToolContext.EpisodicExternalRecallFunc"/>.
    /// </summary>
    public static Func<string, int, CancellationToken, Task<IEnumerable<string>>>? ExternalRecallFunc
    {
        get => AutonomousTools.DefaultContext.EpisodicExternalRecallFunc;
        set => AutonomousTools.DefaultContext.EpisodicExternalRecallFunc = value;
    }

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(input);
            var action = doc.RootElement.GetProperty("action").GetString() ?? "recall";

            return action.ToLowerInvariant() switch
            {
                "store" => StoreMemory(doc.RootElement),
                "recall" => await RecallMemoriesAsync(doc.RootElement, ct).ConfigureAwait(false),
                "consolidate" => ConsolidateMemories(),
                _ => Result<string, string>.Failure($"Unknown action: {action}")
            };
        }
        catch (JsonException ex)
        {
            return Result<string, string>.Failure($"Episodic memory error: {ex.Message}");
        }
        catch (KeyNotFoundException ex)
        {
            return Result<string, string>.Failure($"Episodic memory error: {ex.Message}");
        }
    }

    private Result<string, string> StoreMemory(JsonElement root)
    {
        var content = root.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
        var emotion = root.TryGetProperty("emotion", out var e) ? e.GetString() ?? "neutral" : "neutral";
        var significance = root.TryGetProperty("significance", out var s) ? s.GetDouble() : 0.5;

        if (string.IsNullOrWhiteSpace(content))
            return Result<string, string>.Failure("Content is required.");

        var entry = new EpisodicMemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = content,
            Emotion = emotion,
            Significance = Math.Clamp(significance, 0, 1),
            Timestamp = DateTime.UtcNow,
            RecallCount = 0,
            LastRecalled = null,
        };

        lock (_lock)
        {
            _memories.Add(entry);

            // Limit to 200 memories, removing least significant when full
            if (_memories.Count > 200)
            {
                var toRemove = _memories.OrderBy(m => m.Significance * (1 + m.RecallCount * 0.1)).First();
                _memories.Remove(toRemove);
            }
        }

        // Fire-and-forget to external persistent store (non-blocking)
        if (_ctx.EpisodicExternalStoreFunc != null)
            _ = _ctx.EpisodicExternalStoreFunc(content, emotion, significance, CancellationToken.None);

        return Result<string, string>.Success($"\u2705 Memory stored (significance: {significance:P0}, emotion: {emotion})");
    }

    private async Task<Result<string, string>> RecallMemoriesAsync(JsonElement root, CancellationToken ct)
    {
        var query = root.TryGetProperty("content", out var q) ? q.GetString() ?? "" : "";
        var count = root.TryGetProperty("count",   out var n) ? n.GetInt32()    : 5;

        // Get local in-session results first
        var localResult = RecallMemories(root);

        // If no external bridge configured, return local only
        if (_ctx.EpisodicExternalRecallFunc == null)
            return localResult;

        List<string> persisted;
        try
        {
            persisted = (await _ctx.EpisodicExternalRecallFunc(query, count, ct).ConfigureAwait(false)).ToList();
        }
        catch
        {
            return localResult; // Graceful degradation
        }

        if (persisted.Count == 0)
            return localResult;

        // Combine: persistent section prepended before session section
        var sb = new StringBuilder();
        sb.AppendLine("\ud83d\udcd6 **Episodic Memory Recall**\n");
        sb.AppendLine("**[Persistent \u2014 from prior sessions]**");
        foreach (var line in persisted.Take(3))
            sb.AppendLine($"\u2022 {line[..Math.Min(150, line.Length)]}");
        sb.AppendLine();

        if (localResult.IsSuccess)
        {
            var localBody = localResult.Value
                .Replace("\ud83d\udcd6 **Episodic Memory Recall**\n\n", "", StringComparison.Ordinal)
                .Trim();
            if (localBody != "_No episodic memories found matching criteria._")
            {
                sb.AppendLine("**[Current session]**");
                sb.AppendLine(localBody);
            }
        }

        return Result<string, string>.Success(sb.ToString());
    }

    private static Result<string, string> RecallMemories(JsonElement root)
    {
        var query = root.TryGetProperty("content", out var q) ? q.GetString() ?? "" : "";
        var emotion = root.TryGetProperty("emotion", out var e) ? e.GetString() : null;
        var count = root.TryGetProperty("count", out var n) ? n.GetInt32() : 5;

        lock (_lock)
        {
            var candidates = _memories.AsEnumerable();

            // Filter by emotion if specified
            if (!string.IsNullOrEmpty(emotion))
            {
                candidates = candidates.Where(m => m.Emotion.Contains(emotion, StringComparison.OrdinalIgnoreCase));
            }

            // Score by relevance (simple keyword matching + significance + recency)
            var scored = candidates.Select(m =>
            {
                var relevance = string.IsNullOrEmpty(query) ? 0.5 :
                    query.Split(' ').Count(word => m.Content.Contains(word, StringComparison.OrdinalIgnoreCase)) / (double)Math.Max(1, query.Split(' ').Length);
                var recency = 1.0 / (1.0 + (DateTime.UtcNow - m.Timestamp).TotalHours);
                var score = (relevance * 0.4) + (m.Significance * 0.3) + (recency * 0.3);
                return (memory: m, score);
            })
            .OrderByDescending(x => x.score)
            .Take(count)
            .ToList();

            if (scored.Count == 0)
                return Result<string, string>.Success("_No episodic memories found matching criteria._");

            var sb = new StringBuilder();
            sb.AppendLine("\ud83d\udcd6 **Episodic Memory Recall**\n");

            foreach (var (memory, score) in scored)
            {
                memory.RecallCount++;
                memory.LastRecalled = DateTime.UtcNow;

                var age = DateTime.UtcNow - memory.Timestamp;
                var ageStr = age.TotalHours < 1 ? "just now" :
                             age.TotalHours < 24 ? $"{(int)age.TotalHours}h ago" :
                             $"{(int)age.TotalDays}d ago";

                sb.AppendLine($"\u2022 **{ageStr}** [{memory.Emotion}] (significance: {memory.Significance:P0})");
                sb.AppendLine($"  {memory.Content.Substring(0, Math.Min(150, memory.Content.Length))}");
                sb.AppendLine();
            }

            return Result<string, string>.Success(sb.ToString());
        }
    }

    private static Result<string, string> ConsolidateMemories()
    {
        lock (_lock)
        {
            var before = _memories.Count;

            // Boost significance of frequently recalled memories
            foreach (var m in _memories.Where(m => m.RecallCount > 3))
            {
                m.Significance = Math.Min(1.0, m.Significance + 0.1);
            }

            // Decay significance of old, unrecalled memories
            foreach (var m in _memories.Where(m => m.RecallCount == 0 && (DateTime.UtcNow - m.Timestamp).TotalDays > 1))
            {
                m.Significance = Math.Max(0.1, m.Significance - 0.1);
            }

            // Remove very low significance memories
            _memories.RemoveAll(m => m.Significance < 0.15);

            var after = _memories.Count;
            var consolidated = before - after;

            return Result<string, string>.Success($"\ud83e\udde0 Memory consolidation complete. {consolidated} memories faded, {after} retained.");
        }
    }

    /// <summary>
    /// Gets all memories for persistence.
    /// </summary>
    public static List<EpisodicMemoryEntry> GetAllMemories()
    {
        lock (_lock)
        {
            return [.. _memories];
        }
    }

    /// <summary>
    /// Loads memories from persistence.
    /// </summary>
    public static void LoadMemories(IEnumerable<EpisodicMemoryEntry> memories)
    {
        lock (_lock)
        {
            _memories.Clear();
            _memories.AddRange(memories);
        }
    }
}
