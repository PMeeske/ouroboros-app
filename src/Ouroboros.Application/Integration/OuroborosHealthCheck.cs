// <copyright file="OuroborosHealthChecks.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Health check for the Ouroboros integrated system.
/// Checks availability and health of all engines and subsystems.
/// </summary>
public sealed class OuroborosHealthCheck : IHealthCheck
{
    private readonly IOuroborosCore _core;

    /// <summary>
    /// Initializes a new instance of the <see cref="OuroborosHealthCheck"/> class.
    /// </summary>
    public OuroborosHealthCheck(IOuroborosCore core)
    {
        _core = core ?? throw new ArgumentNullException(nameof(core));
    }

    /// <summary>
    /// Performs health check on the Ouroboros system.
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();
        var unhealthyEngines = new List<string>();

        try
        {
            // Check each engine availability
            CheckEngine("EpisodicMemory", _core.EpisodicMemory, data, unhealthyEngines);
            CheckEngine("AdapterLearning", _core.AdapterLearning, data, unhealthyEngines);
            CheckEngine("MeTTaReasoning", _core.MeTTaReasoning, data, unhealthyEngines);
            CheckEngine("HierarchicalPlanner", _core.HierarchicalPlanner, data, unhealthyEngines);
            CheckEngine("Reflection", _core.Reflection, data, unhealthyEngines);
            CheckEngine("ProgramSynthesis", _core.ProgramSynthesis, data, unhealthyEngines);
            CheckEngine("WorldModel", _core.WorldModel, data, unhealthyEngines);
            CheckEngine("MultiAgent", _core.MultiAgent, data, unhealthyEngines);
            CheckEngine("CausalReasoning", _core.CausalReasoning, data, unhealthyEngines);
            CheckEngine("MetaLearning", _core.MetaLearning, data, unhealthyEngines);
            CheckEngine("EmbodiedAgent", _core.EmbodiedAgent, data, unhealthyEngines);
            CheckEngine("Consciousness", _core.Consciousness, data, unhealthyEngines);
            CheckEngine("Benchmarks", _core.Benchmarks, data, unhealthyEngines);

            if (unhealthyEngines.Any())
            {
                return HealthCheckResult.Degraded(
                    $"Some engines unavailable: {string.Join(", ", unhealthyEngines)}",
                    data: data);
            }

            data["status"] = "All engines operational";
            return HealthCheckResult.Healthy("Ouroboros system operational", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Ouroboros system health check failed",
                ex,
                data);
        }
    }

    private static void CheckEngine(string name, object? engine, Dictionary<string, object> data, List<string> unhealthy)
    {
        if (engine == null)
        {
            data[$"{name}.Status"] = "unavailable";
            unhealthy.Add(name);
        }
        else
        {
            data[$"{name}.Status"] = "available";
            data[$"{name}.Type"] = engine.GetType().Name;
        }
    }
}