// <copyright file="OuroborosServiceCollectionExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Extension methods for registering Ouroboros services with dependency injection.
/// Provides both basic and full registration with builder pattern support.
/// </summary>
public static class OuroborosServiceCollectionExtensions
{
    /// <summary>
    /// Adds basic Ouroboros services with default configuration.
    /// Registers core services without advanced features.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOuroboros(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register event bus (singleton for cross-cutting communication)
        services.TryAddSingleton<IEventBus, EventBus>();

        // Note: Actual engine implementations would need to be registered
        // This is a template showing the registration pattern
        // Each engine interface needs its concrete implementation

        // Example pattern (would need actual implementations):
        // services.TryAddSingleton<IEpisodicMemoryEngine, EpisodicMemoryEngine>();
        // services.TryAddSingleton<IAdapterLearningEngine, AdapterLearningEngine>();
        // etc.

        return services;
    }

    /// <summary>
    /// Adds full Ouroboros system with builder-based configuration.
    /// Provides fluent API for configuring all features.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure the Ouroboros builder.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOuroborosFull(
        this IServiceCollection services,
        Action<IOuroborosBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Create builder
        var builder = new OuroborosBuilder(services);

        // Apply configuration
        configure?.Invoke(builder);

        // Register event bus
        services.TryAddSingleton<IEventBus, EventBus>();

        // Register global workspace (required for consciousness scaffold)
        RegisterGlobalWorkspace(services, builder);

        // Register consciousness scaffold
        RegisterConsciousnessScaffold(services, builder);

        // Register cognitive loop
        RegisterCognitiveLoop(services, builder);

        // Register all engine interfaces (Note: implementations must be provided separately)
        RegisterEngineInterfaces(services);

        // Register OuroborosCore as the unified interface
        services.TryAddSingleton<IOuroborosCore, OuroborosCore>();

        // Build to register all options
        builder.Build();

        return services;
    }

    /// <summary>
    /// Adds Ouroboros with custom engine implementations.
    /// Allows injection of mock or custom implementations for testing.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="registerEngines">Action to register engine implementations.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOuroborosWithEngines(
        this IServiceCollection services,
        Action<IServiceCollection> registerEngines)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(registerEngines);

        // Register event bus
        services.TryAddSingleton<IEventBus, EventBus>();

        // Register custom engines
        registerEngines(services);

        // Register infrastructure components
        services.TryAddSingleton<IConsciousnessScaffold, ConsciousnessScaffold>();
        services.TryAddSingleton<ICognitiveLoop, CognitiveLoop>();

        // Register OuroborosCore
        services.TryAddSingleton<IOuroborosCore, OuroborosCore>();

        return services;
    }

    private static void RegisterGlobalWorkspace(
        IServiceCollection services,
        OuroborosBuilder builder)
    {
        // Note: Actual GlobalWorkspace implementation would be registered here
        // For now, we simply skip using the generic GetOptions method
        // services.TryAddSingleton<IGlobalWorkspace>(sp =>
        // {
        //     var policy = new AttentionPolicy(...);
        //     return new GlobalWorkspace(policy);
        // });
    }

    private static void RegisterConsciousnessScaffold(
        IServiceCollection services,
        OuroborosBuilder builder)
    {
        services.TryAddSingleton<IConsciousnessScaffold, ConsciousnessScaffold>();
    }

    private static void RegisterCognitiveLoop(
        IServiceCollection services,
        OuroborosBuilder builder)
    {
        services.TryAddSingleton<ICognitiveLoop, CognitiveLoop>();
    }

    private static void RegisterEngineInterfaces(IServiceCollection services)
    {
        // Register available Tier 1 engines
        // Note: EpisodicMemoryEngine requires QdrantClient and IEmbeddingModel dependencies
        // Commenting out to avoid DI errors in minimal setup
        // services.TryAddSingleton<IEpisodicMemoryEngine, EpisodicMemoryEngine>();

        // Tier 2 and 3 engines are defined in interface but require implementation
        // Uncomment when implementations become available:
        // services.TryAddSingleton<IAdapterLearningEngine, AdapterLearningEngine>();
        // services.TryAddSingleton<IAdvancedMeTTaEngine, AdvancedMeTTaEngine>();
        // services.TryAddSingleton<IHierarchicalPlanner, HierarchicalPlanner>();
        // services.TryAddSingleton<IReflectionEngine, ReflectionEngine>();
        // services.TryAddSingleton<IBenchmarkSuite, BenchmarkSuite>();
        // services.TryAddSingleton<IProgramSynthesisEngine, ProgramSynthesisEngine>();
        // services.TryAddSingleton<IWorldModelEngine, WorldModelEngine>();
        // services.TryAddSingleton<IMultiAgentCoordinator, MultiAgentCoordinator>();
        // services.TryAddSingleton<ICausalReasoningEngine, CausalReasoningEngine>();
        // services.TryAddSingleton<IMetaLearningEngine, MetaLearningEngine>();
        // services.TryAddSingleton<IEmbodiedAgent, EmbodiedAgent>();
    }
}