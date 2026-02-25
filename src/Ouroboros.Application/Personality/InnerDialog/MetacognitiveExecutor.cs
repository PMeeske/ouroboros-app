namespace Ouroboros.Application.Personality;

/// <summary>
/// Executor that performs metacognitive self-assessment during reflective thoughts.
/// Evaluates performance and identifies improvement opportunities.
/// </summary>
public sealed class MetacognitiveExecutor : IBackgroundOperationExecutor
{
    public string Name => "Metacognitive";
    public IReadOnlyList<string> SupportedOperations => ["self_assessment", "capability_check"];

    public bool ShouldExecute(InnerThoughtType thoughtType, BackgroundOperationContext context)
    {
        return thoughtType == InnerThoughtType.Metacognitive &&
               context.SelfAwareness != null;
    }

    public Task<BackgroundOperationResult?> ExecuteAsync(
        InnerThought thought,
        BackgroundOperationContext context,
        CancellationToken ct = default)
    {
        if (context.SelfAwareness == null) return Task.FromResult<BackgroundOperationResult?>(null);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Assess which capabilities are relevant to current context
        var relevantCapabilities = context.SelfAwareness.Capabilities
            .Where(c =>
                (context.CurrentTopic?.Contains(c, StringComparison.OrdinalIgnoreCase) ?? false) ||
                context.AvailableTools.Any(t => t.Contains(c, StringComparison.OrdinalIgnoreCase)) ||
                context.AvailableSkills.Any(s => s.Contains(c, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var alignmentScore = CalculateAlignmentScore(context);
        var assessment = new
        {
            RelevantCapabilities = relevantCapabilities,
            TotalCapabilities = context.SelfAwareness.Capabilities.Length,
            CurrentFocus = context.CurrentTopic,
            Values = context.SelfAwareness.Values,
            AlignmentScore = alignmentScore
        };

        sw.Stop();

        var result = new BackgroundOperationResult(
            "self_assessment",
            "metacognitive_check",
            true,
            $"Assessed {relevantCapabilities.Count} relevant capabilities for current context",
            assessment,
            sw.Elapsed,
            thought.Type);

        return Task.FromResult<BackgroundOperationResult?>(result);
    }

    private static double CalculateAlignmentScore(BackgroundOperationContext context)
    {
        if (context.SelfAwareness == null) return 0.5;

        // Simple alignment score based on how well current context matches values
        var valueKeywords = context.SelfAwareness.Values
            .SelectMany(v => v.Split('_', '-', ' '))
            .Select(v => v.ToLowerInvariant())
            .ToHashSet();

        var contextKeywords = new List<string>();
        if (context.CurrentTopic != null)
            contextKeywords.AddRange(context.CurrentTopic.Split(' ').Select(w => w.ToLowerInvariant()));
        if (context.LastUserMessage != null)
            contextKeywords.AddRange(context.LastUserMessage.Split(' ').Select(w => w.ToLowerInvariant()));

        if (contextKeywords.Count == 0) return 0.5;

        var matches = contextKeywords.Count(k => valueKeywords.Contains(k));
        return Math.Min(1.0, 0.5 + (matches * 0.1));
    }
}