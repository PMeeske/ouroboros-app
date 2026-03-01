// <copyright file="OuroborosServiceCollectionExtensions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.WorldModel;
using Ouroboros.Agent.MetaLearning;
using Ouroboros.Application.Embodied;
using Ouroboros.Application.Services.Reflection;
using Ouroboros.Core.Configuration;
using Ouroboros.Core.Learning;
using Ouroboros.Core.Reasoning;
using Ouroboros.Core.Synthesis;
using Ouroboros.Domain.Benchmarks;
using Ouroboros.Domain.Embodied;
using Ouroboros.Domain.Learning;
using Ouroboros.Domain.MetaLearning;
using Ouroboros.Domain.MultiAgent;
using Ouroboros.Domain.Reflection;
using Ouroboros.Network.Persistence;
using Ouroboros.Pipeline.Memory;
using Ouroboros.Tools.MeTTa;
using Qdrant.Client;

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
        // ── Tier 1: EpisodicMemoryEngine (Qdrant-backed) ────────────────────
        services.TryAddSingleton<IEpisodicMemoryEngine>(sp =>
        {
            var client = sp.GetService<QdrantClient>();
            var registry = sp.GetService<IQdrantCollectionRegistry>();
            var embedding = sp.GetService<Ouroboros.Domain.IEmbeddingModel>();
            if (client != null && registry != null && embedding != null)
                return new EpisodicMemoryEngine(client, registry, embedding);
            return NullEpisodicMemoryEngine.Instance;
        });

        // ── Tier 1: AdapterLearningEngine (needs PEFT + storage) ────────────
        services.TryAddSingleton<IAdapterLearningEngine>(sp =>
        {
            var peft = sp.GetService<IPeftIntegration>();
            var storage = sp.GetService<IAdapterStorage>();
            var blob = sp.GetService<IAdapterBlobStorage>();
            if (peft != null && storage != null && blob != null)
                return new AdapterLearningEngine(peft, storage, blob, "ouroboros-base",
                    sp.GetService<ILogger<AdapterLearningEngine>>());
            return NullAdapterLearningEngine.Instance;
        });

        // ── Tier 1: AdvancedMeTTaEngine (needs IMeTTaEngine) ────────────────
        services.TryAddSingleton<IAdvancedMeTTaEngine>(sp =>
        {
            var metta = sp.GetService<IMeTTaEngine>();
            if (metta != null)
                return new AdvancedMeTTaEngine(metta);
            return NullAdvancedMeTTaEngine.Instance;
        });

        // ── Tier 1: HierarchicalPlanner (needs orchestrator + LLM) ──────────
        services.TryAddSingleton<IHierarchicalPlanner>(sp =>
        {
            var orchestrator = sp.GetService<IMetaAIPlannerOrchestrator>();
            var llm = sp.GetService<Ouroboros.Abstractions.Core.IChatCompletionModel>();
            if (orchestrator != null && llm != null)
                return new HierarchicalPlanner(orchestrator, llm);
            return NullHierarchicalPlanner.Instance;
        });

        // ── Tier 1: ReflectionEngine (parameterless) ────────────────────────
        services.TryAddSingleton<IReflectionEngine, ReflectionEngine>();

        // ── Tier 1: BenchmarkSuite (parameterless) ──────────────────────────
        services.TryAddSingleton<IBenchmarkSuite, BenchmarkSuite>();

        // ── Tier 2: ProgramSynthesisEngine (parameterless) ──────────────────
        services.TryAddSingleton<IProgramSynthesisEngine, ProgramSynthesisEngine>();

        // ── Tier 2: WorldModelEngine (parameterless) ────────────────────────
        services.TryAddSingleton<IWorldModelEngine, WorldModelEngine>();

        // ── Tier 2: MultiAgentCoordinator (needs message queue + registry) ──
        services.TryAddSingleton<IMultiAgentCoordinator>(sp =>
        {
            var queue = sp.GetService<IMessageQueue>();
            var agentRegistry = sp.GetService<IAgentRegistry>();
            if (queue != null && agentRegistry != null)
                return new MultiAgentCoordinator(queue, agentRegistry);
            return NullMultiAgentCoordinator.Instance;
        });

        // ── Tier 2: CausalReasoningEngine (parameterless) ───────────────────
        services.TryAddSingleton<ICausalReasoningEngine, CausalReasoningEngine>();

        // ── Tier 3: MetaLearningEngine (needs IEmbeddingModel) ──────────────
        services.TryAddSingleton<IMetaLearningEngine>(sp =>
        {
            var embedding = sp.GetService<Ouroboros.Domain.IEmbeddingModel>();
            if (embedding != null)
                return new MetaLearningEngine(embedding);
            return NullMetaLearningEngine.Instance;
        });

        // ── Tier 3: EmbodiedAgent (needs environment + ethics) ──────────────
        services.TryAddSingleton<IEmbodiedAgent>(sp =>
        {
            var env = sp.GetService<IEnvironmentManager>();
            var ethics = sp.GetService<Ouroboros.Core.Ethics.IEthicsFramework>();
            var logger = sp.GetService<ILogger<EmbodiedAgent>>();
            if (env != null && ethics != null && logger != null)
                return new EmbodiedAgent(env, ethics, logger);
            return NullEmbodiedAgent.Instance;
        });

        // ── Network persistence (file-based WAL for Merkle-DAG) ─────────────
        services.TryAddSingleton<IGraphPersistence>(_ =>
            new FileWalPersistence(
                Path.Combine(Path.GetTempPath(), "ouroboros", "wal.ndjson")));
    }
}