using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Commands.Handlers;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Services;
using Ouroboros.Core.CognitivePhysics;

namespace Ouroboros.CLI.Hosting;

/// <summary>
/// Extension methods for service registration.
/// </summary>
public static class ServiceCollectionExtensions
{
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
    /// Registers Cognitive Physics Engine dependencies.
    /// </summary>
    public static IServiceCollection AddCognitivePhysicsDefaults(this IServiceCollection services)
    {
#pragma warning disable CS0618 // Obsolete IEmbeddingProvider/IEthicsGate — CPE still requires them
        services.TryAddSingleton<IEthicsGate, PermissiveEthicsGate>();
        services.TryAddSingleton<IEmbeddingProvider>(sp =>
            new NullEmbeddingProvider());
#pragma warning restore CS0618
        services.TryAddSingleton<CognitivePhysicsConfig>(CognitivePhysicsConfig.Default);
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
}

#pragma warning restore CS0618
