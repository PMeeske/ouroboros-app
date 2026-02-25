using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ouroboros.Application.Integration;

/// <summary>
/// Health check for the episodic memory engine.
/// </summary>
public sealed class EpisodicMemoryHealthCheck : IHealthCheck
{
    private readonly IOuroborosCore _core;

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodicMemoryHealthCheck"/> class.
    /// </summary>
    public EpisodicMemoryHealthCheck(IOuroborosCore core)
    {
        _core = core ?? throw new ArgumentNullException(nameof(core));
    }

    /// <summary>
    /// Checks health of episodic memory system.
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_core.EpisodicMemory == null)
            {
                return HealthCheckResult.Unhealthy("Episodic memory engine not initialized");
            }

            // Try a basic retrieval operation with timeout
            var stopwatch = Stopwatch.StartNew();
            var result = await _core.EpisodicMemory.RetrieveSimilarEpisodesAsync(
                "health_check",
                topK: 1,
                minSimilarity: 0.0,
                ct: cancellationToken);
            stopwatch.Stop();

            return result.Match(
                success => HealthCheckResult.Healthy(
                    "Episodic memory operational",
                    new Dictionary<string, object>
                    {
                        ["response_time_ms"] = stopwatch.ElapsedMilliseconds,
                        ["episodes_retrieved"] = success.Count
                    }),
                error => HealthCheckResult.Degraded(
                    $"Episodic memory degraded: {error}",
                    data: new Dictionary<string, object> { ["error"] = error }));
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Episodic memory health check failed", ex);
        }
    }
}