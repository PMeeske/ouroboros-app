using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ouroboros.Application.Integration;

/// <summary>
/// Health check for the cognitive loop.
/// </summary>
public sealed class CognitiveLoopHealthCheck : IHealthCheck
{
    private readonly ICognitiveLoop? _cognitiveLoop;

    /// <summary>
    /// Initializes a new instance of the <see cref="CognitiveLoopHealthCheck"/> class.
    /// </summary>
    public CognitiveLoopHealthCheck(IServiceProvider serviceProvider)
    {
        // Cognitive loop may not be registered
        _cognitiveLoop = serviceProvider.GetService(typeof(ICognitiveLoop)) as ICognitiveLoop;
    }

    /// <summary>
    /// Checks health of cognitive loop if enabled.
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_cognitiveLoop == null)
            {
                return HealthCheckResult.Healthy(
                    "Cognitive loop not enabled",
                    new Dictionary<string, object> { ["enabled"] = false });
            }

            var state = _cognitiveLoop.GetCurrentState();

            return state.IsRunning
                ? HealthCheckResult.Healthy(
                    "Cognitive loop running",
                    new Dictionary<string, object>
                    {
                        ["enabled"] = true,
                        ["is_running"] = state.IsRunning,
                        ["cycles_completed"] = state.CyclesCompleted,
                        ["last_cycle_time"] = state.LastCycleTime,
                        ["current_phase"] = state.CurrentPhase,
                        ["recent_actions"] = state.RecentActions.Count
                    })
                : HealthCheckResult.Healthy(
                    "Cognitive loop stopped",
                    new Dictionary<string, object>
                    {
                        ["enabled"] = true,
                        ["is_running"] = false,
                        ["cycles_completed"] = state.CyclesCompleted
                    });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cognitive loop health check failed", ex);
        }
    }
}