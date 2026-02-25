namespace Ouroboros.Application.Services;

/// <summary>
/// Result of AGI warmup operation.
/// </summary>
public record WarmupResult
{
    /// <summary>
    /// Whether warmup completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if warmup failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Duration of warmup.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// End time.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Steps performed during warmup.
    /// </summary>
    public List<string> Steps { get; set; } = [];

    /// <summary>
    /// Whether thinking capability is ready.
    /// </summary>
    public bool ThinkingReady { get; set; }

    /// <summary>
    /// Whether search capability is ready.
    /// </summary>
    public bool SearchReady { get; set; }

    /// <summary>
    /// Whether tool execution is ready.
    /// </summary>
    public bool ToolsReady { get; set; }

    /// <summary>
    /// Whether self-awareness is ready.
    /// </summary>
    public bool SelfAwarenessReady { get; set; }

    /// <summary>
    /// Initial warmup thought generated.
    /// </summary>
    public string? WarmupThought { get; set; }

    /// <summary>
    /// Seed thoughts for autonomous operation.
    /// </summary>
    public List<string> SeedThoughts { get; set; } = [];

    /// <summary>
    /// Knowledge reorganization stats.
    /// </summary>
    public ReorganizationStats? KnowledgeStats { get; set; }

    /// <summary>
    /// Total number of tools registered in the system.
    /// </summary>
    public int TotalToolsRegistered { get; set; }

    /// <summary>
    /// Number of tools that were tested during warmup.
    /// </summary>
    public int ToolsTestedCount { get; set; }

    /// <summary>
    /// Number of tools that passed warmup testing.
    /// </summary>
    public int ToolsSuccessCount { get; set; }

    /// <summary>
    /// Per-tool warmup results (tool name -> success).
    /// </summary>
    public Dictionary<string, bool> ToolWarmupResults { get; set; } = [];

    /// <summary>
    /// Whether self-index is warmed up and ready.
    /// </summary>
    public bool SelfIndexReady { get; set; }

    /// <summary>
    /// Self-index warmup statistics.
    /// </summary>
    public SelfIndexWarmupStats? SelfIndexStats { get; set; }

    /// <summary>
    /// Summary of warmup readiness.
    /// </summary>
    public string Summary => Success
        ? $"AGI warmup complete in {Duration.TotalSeconds:F1}s: Thinking={ThinkingReady}, Search={SearchReady}, Tools={ToolsSuccessCount}/{ToolsTestedCount}, Self-Index={SelfIndexReady}, Self-Aware={SelfAwarenessReady}"
        : $"AGI warmup failed: {Error}";
}