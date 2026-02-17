namespace Ouroboros.Application.Personality;

/// <summary>
/// Executor that prefetches related topics and context based on curiosity thoughts.
/// Anticipates user needs by exploring related concepts.
/// </summary>
public sealed class CuriosityPrefetchExecutor : IBackgroundOperationExecutor
{
    private readonly Func<string, CancellationToken, Task<List<string>>>? _topicExplorer;
    private readonly HashSet<string> _exploredTopics = [];

    public string Name => "CuriosityPrefetch";
    public IReadOnlyList<string> SupportedOperations => ["topic_exploration", "related_concepts"];

    public CuriosityPrefetchExecutor(Func<string, CancellationToken, Task<List<string>>>? topicExplorer = null)
    {
        _topicExplorer = topicExplorer;
    }

    public bool ShouldExecute(InnerThoughtType thoughtType, BackgroundOperationContext context)
    {
        return thoughtType == InnerThoughtType.Curiosity &&
               !string.IsNullOrEmpty(context.CurrentTopic) &&
               !_exploredTopics.Contains(context.CurrentTopic);
    }

    public async Task<BackgroundOperationResult?> ExecuteAsync(
        InnerThought thought,
        BackgroundOperationContext context,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(context.CurrentTopic)) return null;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        _exploredTopics.Add(context.CurrentTopic);

        // Keep explored topics bounded
        while (_exploredTopics.Count > 100)
        {
            _exploredTopics.Remove(_exploredTopics.First());
        }

        List<string> relatedTopics;
        if (_topicExplorer != null)
        {
            relatedTopics = await _topicExplorer(context.CurrentTopic, ct);
        }
        else
        {
            // Default: generate related concepts based on context
            relatedTopics = GenerateRelatedConcepts(context.CurrentTopic, context);
        }

        sw.Stop();

        return new BackgroundOperationResult(
            "topic_exploration",
            context.CurrentTopic,
            true,
            $"Explored {relatedTopics.Count} related concepts",
            new Dictionary<string, object>
            {
                ["topic"] = context.CurrentTopic,
                ["related"] = relatedTopics,
                ["thought_content"] = thought.Content
            },
            sw.Elapsed,
            thought.Type);
    }

    private static List<string> GenerateRelatedConcepts(string topic, BackgroundOperationContext context)
    {
        // Simple related concept generation based on profile and topic
        var concepts = new List<string>();

        if (context.Profile?.CuriosityDrivers != null)
        {
            concepts.AddRange(
                context.Profile.CuriosityDrivers
                    .Where(c => c.Topic.Contains(topic, StringComparison.OrdinalIgnoreCase) ||
                                topic.Contains(c.Topic, StringComparison.OrdinalIgnoreCase))
                    .SelectMany(c => new[] { c.Topic, $"{topic} and {c.Topic}" }));
        }

        if (context.SelfAwareness?.Capabilities != null)
        {
            concepts.AddRange(
                context.SelfAwareness.Capabilities
                    .Select(c => $"{topic} using {c}")
                    .Take(3));
        }

        return concepts.Distinct().ToList();
    }
}