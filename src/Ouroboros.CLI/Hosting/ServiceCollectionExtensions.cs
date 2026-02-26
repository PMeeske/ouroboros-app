using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ouroboros.ApiHost.Extensions;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Commands.Handlers;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Mediator;
using Ouroboros.CLI.Services;

namespace Ouroboros.CLI.Hosting;

/// <summary>
/// Extension methods for CLI service registration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the full CLI host: shared engine + foundational dependencies
    /// (cognitive physics, self-model, health checks) followed by all CLI-specific
    /// services, command handlers, and infrastructure. This is the single entry
    /// point for bootstrapping the CLI's DI container and mirrors the Web API's
    /// <see cref="WebApiServiceCollectionExtensions.AddOuroborosWebApi"/> pattern.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <returns><paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddCliHost(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        // ── Shared engine + foundational dependencies ────────────────────────
        // Cognitive physics, self-model, health checks — same call the Web API uses.
        services.AddOuroborosEngine(configuration);

        // ── MediatR: CLI request/response bus ────────────────────────────────
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<AskQueryHandler>());

        // ── CLI-specific services ────────────────────────────────────────────
        services.AddCliServices();
        services.AddCommandHandlers();
        services.AddInfrastructureServices();
        services.AddExistingBusinessLogic();

        return services;
    }

    /// <summary>
    /// Registers CLI business-logic services.
    /// Services are registered as <c>Transient</c> because System.CommandLine
    /// does not create a DI scope per command invocation — resolving Scoped
    /// services from the root provider would cause them to behave as singletons.
    /// </summary>
    public static IServiceCollection AddCliServices(this IServiceCollection services)
    {
        services.TryAddTransient<IAskService, AskService>();
        services.TryAddTransient<IPipelineService, PipelineService>();
        services.TryAddTransient<IOuroborosAgentService, OuroborosAgentService>();
        services.TryAddTransient<IImmersiveModeService, ImmersiveModeService>();
        services.TryAddTransient<IRoomModeService, RoomModeService>();
        services.TryAddTransient<ISkillsService, SkillsService>();
        services.TryAddTransient<IOrchestratorService, OrchestratorService>();
        services.TryAddTransient<ICognitivePhysicsService, CognitivePhysicsService>();
        services.TryAddTransient<IMeTTaService, MeTTaService>();
        return services;
    }

    /// <summary>
    /// Registers all command handlers as <c>Transient</c>.
    /// </summary>
    public static IServiceCollection AddCommandHandlers(this IServiceCollection services)
    {
        services.AddTransient<AskCommandHandler>();
        services.AddTransient<PipelineCommandHandler>();
        services.AddTransient<OuroborosCommandHandler>();
        services.AddTransient<ImmersiveCommandHandler>();
        services.AddTransient<RoomCommandHandler>();
        services.AddTransient<SkillsCommandHandler>();
        services.AddTransient<OrchestratorCommandHandler>();
        services.AddTransient<CognitivePhysicsCommandHandler>();
        services.AddTransient<QualityCommandHandler>();
        services.AddTransient<MeTTaCommandHandler>();
        services.AddTransient<ClaudeCheckCommandHandler>();
        return services;
    }

    /// <summary>
    /// Registers infrastructure services (console, voice, cloud sync).
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.TryAddSingleton<ISpectreConsoleService, SpectreConsoleService>();
        services.TryAddTransient<IVoiceIntegrationService, VoiceIntegrationService>();
        services.TryAddSingleton<Ouroboros.ApiHost.Services.IQdrantSyncService, Ouroboros.ApiHost.Services.QdrantSyncService>();
        return services;
    }

    /// <summary>
    /// Registers existing business logic services.
    /// </summary>
    public static IServiceCollection AddExistingBusinessLogic(this IServiceCollection services)
    {
        services.TryAddSingleton(new VoiceModeConfig());
        services.TryAddTransient<VoiceModeService>();
        return services;
    }

    /// <summary>
    /// Redirects <see cref="IAskService"/> and <see cref="IPipelineService"/> to
    /// call a remote Ouroboros Web API instead of running the pipeline locally.
    /// This makes the API a complete <em>upstream provider</em> for the CLI.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <param name="apiBaseUrl">
    /// Base URL of the running Ouroboros API, e.g. <c>http://localhost:5000</c>.
    /// Typically supplied via the CLI's <c>--api-url</c> option.
    /// </param>
    /// <returns><paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddUpstreamApiProvider(
        this IServiceCollection services,
        string apiBaseUrl)
    {
        // Register the typed HTTP client that talks to the upstream API
        services.AddOuroborosApiClient(apiBaseUrl);

        // Override the local service implementations with HTTP-backed ones.
        // Uses Transient to match AddCliServices() — see lifetime rationale there.
        services.AddTransient<IAskService, HttpApiAskService>();
        services.AddTransient<IPipelineService, HttpApiPipelineService>();

        return services;
    }
}
