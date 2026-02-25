namespace Ouroboros.Application.Personality;

/// <summary>
/// Executor that consolidates and synthesizes recent conversation patterns.
/// Runs during consolidation thoughts to prepare summaries.
/// </summary>
public sealed class ConsolidationExecutor : IBackgroundOperationExecutor
{
    public string Name => "Consolidation";
    public IReadOnlyList<string> SupportedOperations => ["pattern_synthesis", "context_summary"];

    public bool ShouldExecute(InnerThoughtType thoughtType, BackgroundOperationContext context)
    {
        return thoughtType == InnerThoughtType.Consolidation &&
               context.RecentTopics.Count > 2;
    }

    public Task<BackgroundOperationResult?> ExecuteAsync(
        InnerThought thought,
        BackgroundOperationContext context,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Find common themes across recent topics
        var topicWords = context.RecentTopics
            .SelectMany(t => t.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Select(w => w.ToLowerInvariant().Trim())
            .Where(w => w.Length > 3)
            .GroupBy(w => w)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => g.Key)
            .ToList();

        var synthesis = new
        {
            CommonThemes = topicWords,
            TopicCount = context.RecentTopics.Count,
            TopicFlow = string.Join(" → ", context.RecentTopics.TakeLast(5)),
            SuggestedFocus = topicWords.FirstOrDefault() ?? context.CurrentTopic
        };

        sw.Stop();

        var result = new BackgroundOperationResult(
            "pattern_synthesis",
            "conversation_consolidation",
            true,
            $"Synthesized {topicWords.Count} common themes from {context.RecentTopics.Count} topics",
            synthesis,
            sw.Elapsed,
            thought.Type);

        return Task.FromResult<BackgroundOperationResult?>(result);
    }
}