using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ouroboros.Application.Integration;

/// <summary>
/// Health check for the consciousness scaffold.
/// </summary>
public sealed class ConsciousnessHealthCheck : IHealthCheck
{
    private readonly IOuroborosCore _core;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsciousnessHealthCheck"/> class.
    /// </summary>
    public ConsciousnessHealthCheck(IOuroborosCore core)
    {
        _core = core ?? throw new ArgumentNullException(nameof(core));
    }

    /// <summary>
    /// Checks health of consciousness scaffold.
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_core.Consciousness == null)
            {
                return HealthCheckResult.Unhealthy("Consciousness scaffold not initialized");
            }

            // Check workspace availability
            var workspace = _core.Consciousness.GlobalWorkspace;
            if (workspace == null)
            {
                return HealthCheckResult.Degraded("Global workspace not available");
            }

            var items = workspace.GetItems();
            var highPriority = workspace.GetHighPriorityItems();

            return HealthCheckResult.Healthy(
                "Consciousness scaffold operational",
                new Dictionary<string, object>
                {
                    ["workspace_items"] = items.Count,
                    ["high_priority_items"] = highPriority.Count
                });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Consciousness health check failed", ex);
        }
    }
}