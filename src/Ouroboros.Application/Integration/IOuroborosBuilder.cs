// <copyright file="IOuroborosBuilder.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Builder interface for fluent configuration of the Ouroboros system.
/// Follows the builder pattern with method chaining for composable configuration.
/// </summary>
public interface IOuroborosBuilder
{
    /// <summary>Gets the service collection for dependency injection.</summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Configures episodic memory with the specified options.
    /// </summary>
    /// <param name="configure">Action to configure episodic memory options.</param>
    /// <returns>The builder for method chaining.</returns>
    IOuroborosBuilder WithEpisodicMemory(Action<EpisodicMemoryOptions>? configure = null);

    /// <summary>
    /// Configures adapter learning with the specified options.
    /// </summary>
    /// <param name="configure">Action to configure adapter learning options.</param>
    /// <returns>The builder for method chaining.</returns>
    IOuroborosBuilder WithAdapterLearning(Action<AdapterLearningOptions>? configure = null);

    /// <summary>
    /// Configures MeTTa reasoning with the specified options.
    /// </summary>
    /// <param name="configure">Action to configure MeTTa reasoning options.</param>
    /// <returns>The builder for method chaining.</returns>
    IOuroborosBuilder WithMeTTaReasoning(Action<MeTTaReasoningOptions>? configure = null);

    /// <summary>
    /// Configures hierarchical planning with the specified options.
    /// </summary>
    /// <param name="configure">Action to configure hierarchical planning options.</param>
    /// <returns>The builder for method chaining.</returns>
    IOuroborosBuilder WithHierarchicalPlanning(Action<HierarchicalPlanningOptions>? configure = null);

    /// <summary>
    /// Configures reflection with the specified options.
    /// </summary>
    /// <param name="configure">Action to configure reflection options.</param>
    /// <returns>The builder for method chaining.</returns>
    IOuroborosBuilder WithReflection(Action<ReflectionOptions>? configure = null);

    /// <summary>
    /// Configures program synthesis with the specified options.
    /// </summary>
    /// <param name="configure">Action to configure program synthesis options.</param>
    /// <returns>The builder for method chaining.</returns>
    IOuroborosBuilder WithProgramSynthesis(Action<ProgramSynthesisOptions>? configure = null);

    /// <summary>
    /// Configures world model with the specified options.
    /// </summary>
    /// <param name="configure">Action to configure world model options.</param>
    /// <returns>The builder for method chaining.</returns>
    IOuroborosBuilder WithWorldModel(Action<WorldModelOptions>? configure = null);

    /// <summary>
    /// Configures multi-agent coordination with the specified options.
    /// </summary>
    /// <param name="configure">Action to configure multi-agent options.</param>
    /// <returns>The builder for method chaining.</returns>
    IOuroborosBuilder WithMultiAgent(Action<MultiAgentOptions>? configure = null);

    /// <summary>
    /// Configures causal reasoning with the specified options.
    /// </summary>
    /// <param name="configure">Action to configure causal reasoning options.</param>
    /// <returns>The builder for method chaining.</returns>
    IOuroborosBuilder WithCausalReasoning(Action<CausalReasoningOptions>? configure = null);

    /// <summary>
    /// Configures meta-learning with the specified options.
    /// </summary>
    /// <param name="configure">Action to configure meta-learning options.</param>
    /// <returns>The builder for method chaining.</returns>
    IOuroborosBuilder WithMetaLearning(Action<MetaLearningOptions>? configure = null);

    /// <summary>
    /// Configures embodied agent with the specified options.
    /// </summary>
    /// <param name="configure">Action to configure embodied agent options.</param>
    /// <returns>The builder for method chaining.</returns>
    IOuroborosBuilder WithEmbodiedAgent(Action<EmbodiedAgentOptions>? configure = null);

    /// <summary>
    /// Configures consciousness scaffold with the specified options.
    /// </summary>
    /// <param name="configure">Action to configure consciousness options.</param>
    /// <returns>The builder for method chaining.</returns>
    IOuroborosBuilder WithConsciousness(Action<ConsciousnessOptions>? configure = null);

    /// <summary>
    /// Configures cognitive loop with the specified options.
    /// </summary>
    /// <param name="configure">Action to configure cognitive loop options.</param>
    /// <returns>The builder for method chaining.</returns>
    IOuroborosBuilder WithCognitiveLoop(Action<CognitiveLoopOptions>? configure = null);

    /// <summary>
    /// Builds and registers all configured services.
    /// </summary>
    /// <returns>The service collection with all Ouroboros services registered.</returns>
    IServiceCollection Build();
}

// ===== Configuration Option Records =====