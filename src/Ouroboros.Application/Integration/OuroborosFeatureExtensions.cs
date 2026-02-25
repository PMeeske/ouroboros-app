using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ouroboros.Pipeline.Memory;

namespace Ouroboros.Application.Integration;

/// <summary>
/// Extension methods for configuring specific Ouroboros features.
/// </summary>
public static class OuroborosFeatureExtensions
{
    /// <summary>
    /// Adds episodic memory services with configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEpisodicMemory(
        this IServiceCollection services,
        Action<EpisodicMemoryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new EpisodicMemoryOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);

        // Register episodic memory engine implementation
        services.TryAddSingleton<IEpisodicMemoryEngine, EpisodicMemoryEngine>();

        return services;
    }

    /// <summary>
    /// Adds consciousness scaffold services with configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddConsciousness(
        this IServiceCollection services,
        Action<ConsciousnessOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = ConsciousnessOptions.Default;
        configure?.Invoke(options);

        services.AddSingleton(options);

        // Register event bus if not already registered
        services.TryAddSingleton<IEventBus, EventBus>();

        // Register global workspace
        // services.TryAddSingleton<IGlobalWorkspace, GlobalWorkspace>();

        // Register consciousness scaffold
        services.TryAddSingleton<IConsciousnessScaffold, ConsciousnessScaffold>();

        return services;
    }

    /// <summary>
    /// Adds cognitive loop services with configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCognitiveLoop(
        this IServiceCollection services,
        Action<CognitiveLoopOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = CognitiveLoopOptions.Default;
        configure?.Invoke(options);

        services.AddSingleton(options);

        // Register cognitive loop
        services.TryAddSingleton<ICognitiveLoop, CognitiveLoop>();

        return services;
    }

    /// <summary>
    /// Adds health checks for all Ouroboros subsystems.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOuroborosHealthChecks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHealthChecks()
            .AddCheck<OuroborosHealthCheck>(
                "ouroboros_system",
                tags: new[] { "ouroboros", "system" })
            .AddCheck<EpisodicMemoryHealthCheck>(
                "ouroboros_episodic_memory",
                tags: new[] { "ouroboros", "memory" })
            .AddCheck<ConsciousnessHealthCheck>(
                "ouroboros_consciousness",
                tags: new[] { "ouroboros", "consciousness" })
            .AddCheck<CognitiveLoopHealthCheck>(
                "ouroboros_cognitive_loop",
                tags: new[] { "ouroboros", "loop" });

        return services;
    }

    /// <summary>
    /// Adds OpenTelemetry instrumentation for Ouroboros operations.
    /// Includes metrics, tracing, and activity tracking.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOuroborosTelemetry(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register telemetry as singleton
        services.TryAddSingleton<OuroborosTelemetry>();

        return services;
    }

    /// <summary>
    /// Adds full Ouroboros system with health checks and telemetry enabled.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureHealthChecks">Optional health check configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOuroborosFullWithMonitoring(
        this IServiceCollection services,
        Action<IHealthChecksBuilder>? configureHealthChecks = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Add full Ouroboros system
        services.AddOuroborosFull();

        // Add health checks
        var healthChecksBuilder = services.AddHealthChecks()
            .AddCheck<OuroborosHealthCheck>(
                "ouroboros_system",
                tags: new[] { "ouroboros", "system", "ready" })
            .AddCheck<EpisodicMemoryHealthCheck>(
                "ouroboros_episodic_memory",
                tags: new[] { "ouroboros", "memory" })
            .AddCheck<ConsciousnessHealthCheck>(
                "ouroboros_consciousness",
                tags: new[] { "ouroboros", "consciousness" })
            .AddCheck<CognitiveLoopHealthCheck>(
                "ouroboros_cognitive_loop",
                tags: new[] { "ouroboros", "loop" });

        // Allow custom health check configuration
        configureHealthChecks?.Invoke(healthChecksBuilder);

        // Add telemetry
        services.TryAddSingleton<OuroborosTelemetry>();

        return services;
    }
}