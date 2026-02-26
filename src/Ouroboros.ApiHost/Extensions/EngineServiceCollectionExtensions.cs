// <copyright file="EngineServiceCollectionExtensions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.SelfModel;
using Ouroboros.ApiHost.Services;
using Ouroboros.Core.CognitivePhysics;
using Ouroboros.Providers;

namespace Ouroboros.ApiHost.Extensions;

/// <summary>
/// <see cref="IServiceCollection"/> extensions that register all Ouroboros engine
/// and foundational dependencies. Call <c>AddOuroborosEngine()</c> from any host –
/// CLI, standalone WebApi, or Android – to get the shared cognitive physics,
/// self-model, and health check subsystems.
/// </summary>
public static class EngineServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Ouroboros engine and foundational dependencies shared by
    /// every host (CLI, Web API, Android). This includes:
    /// <list type="bullet">
    ///   <item>Cognitive Physics Engine defaults (ethics gate, embedding provider, config)</item>
    ///   <item>Self-model components (global workspace, predictive monitor, identity graph)</item>
    ///   <item>Self-model service</item>
    ///   <item>Health checks</item>
    /// </list>
    /// Host-specific services (Swagger, CORS, CLI commands, etc.) are registered
    /// separately by <see cref="WebApiServiceCollectionExtensions.AddOuroborosWebApi"/>
    /// or the CLI's own extension methods.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <param name="configuration">Application configuration root (used for Qdrant settings).</param>
    /// <returns><paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddOuroborosEngine(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        // ── Qdrant cross-cutting infrastructure ──────────────────────────────
        if (configuration != null)
        {
            services.AddQdrant(configuration);
            services.AddQdrantServices();
        }

        // ── Cognitive Physics Engine defaults ─────────────────────────────────
#pragma warning disable CS0618 // Obsolete IEmbeddingProvider/IEthicsGate — CPE still requires them
        services.TryAddSingleton<IEthicsGate, PermissiveEthicsGate>();
        services.TryAddSingleton<IEmbeddingProvider>(
            _ => new NullEmbeddingProvider());
#pragma warning restore CS0618
        services.TryAddSingleton<CognitivePhysicsConfig>(CognitivePhysicsConfig.Default);

        // ── Self-model components (Phase 2) ──────────────────────────────────
        services.TryAddSingleton<IGlobalWorkspace>(_ => new GlobalWorkspace());
        services.TryAddSingleton<IPredictiveMonitor>(_ => new PredictiveMonitor());

        services.TryAddSingleton<IIdentityGraph>(_ =>
        {
            var registry = new CapabilityRegistry(
                new MockChatModel(),
                new ToolRegistry(),
                new CapabilityRegistryConfig());

            return new IdentityGraph(
                Guid.NewGuid(),
                "OuroborosAgent",
                registry,
                Path.Combine(Path.GetTempPath(), "ouroboros_identity.json"));
        });

        services.TryAddSingleton<ISelfModelService, SelfModelService>();

        // ── Health checks (Kubernetes liveness/readiness probes) ─────────────
        services.AddHealthChecks();

        return services;
    }
}
