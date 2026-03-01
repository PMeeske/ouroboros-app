using Ouroboros.Agent.MetaAI;
using Ouroboros.Pipeline.Memory;

namespace Ouroboros.Application.Integration;

/// <summary>
/// Result of goal execution.
/// </summary>
public sealed record PlanExecutionResult(
    bool Success,
    string Output,
    PipelineBranch ReasoningTrace,
    Plan? ExecutedPlan,
    IReadOnlyList<Episode> GeneratedEpisodes,
    TimeSpan Duration);