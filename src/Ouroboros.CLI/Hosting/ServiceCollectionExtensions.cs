using MediatR;
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
    public static IServiceCollection AddCliHost(this IServiceCollection services)
    {
        // ── Shared engine + foundational dependencies ────────────────────────
        // Cognitive physics, self-model, health checks — same call the Web API uses.
        services.AddOuroborosEngine();

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
    /// </summary>
    public static IServiceCollection AddCliServices(this IServiceCollection services)
    {
        services.TryAddScoped<IAskService, AskService>();
        services.TryAddScoped<IPipelineService, PipelineService>();
        services.TryAddScoped<IOuroborosAgentService, OuroborosAgentService>();
        services.TryAddScoped<ISkillsService, SkillsService>();
        services.TryAddScoped<IOrchestratorService, OrchestratorService>();
        services.TryAddScoped<ICognitivePhysicsService, CognitivePhysicsService>();
        return services;
    }

    /// <summary>
    /// Registers all command handlers.
    /// </summary>
    public static IServiceCollection AddCommandHandlers(this IServiceCollection services)
    {
        services.AddScoped<AskCommandHandler>();
        services.AddScoped<PipelineCommandHandler>();
        services.AddScoped<OuroborosCommandHandler>();
        services.AddScoped<SkillsCommandHandler>();
        services.AddScoped<OrchestratorCommandHandler>();
        services.AddScoped<CognitivePhysicsCommandHandler>();
        services.AddScoped<QualityCommandHandler>();
        services.AddScoped<MeTTaCommandHandler>();
        return services;
    }

    /// <summary>
    /// Registers infrastructure services (console, voice).
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.TryAddSingleton<ISpectreConsoleService, SpectreConsoleService>();
        services.TryAddScoped<IVoiceIntegrationService, VoiceIntegrationService>();
        return services;
    }

    /// <summary>
    /// Registers existing business logic services.
    /// </summary>
    public static IServiceCollection AddExistingBusinessLogic(this IServiceCollection services)
    {
        services.TryAddScoped<VoiceModeService>();
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

        // Override the local service implementations with HTTP-backed ones
        services.AddScoped<IAskService, HttpApiAskService>();
        services.AddScoped<IPipelineService, HttpApiPipelineService>();

        return services;
    }
}
