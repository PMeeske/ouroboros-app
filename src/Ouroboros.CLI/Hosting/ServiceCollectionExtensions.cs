
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Services;
using Ouroboros.CLI.Commands.Handlers;
using Ouroboros.Core.CognitivePhysics;

namespace Ouroboros.CLI.Hosting;

/// <summary>
/// Extension methods for service registration
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers CLI-specific services
    /// </summary>
    public static IServiceCollection AddCliServices(this IServiceCollection services)
    {
        // Register command services
        services.TryAddScoped<IAskService, AskService>();
        services.TryAddScoped<IPipelineService, PipelineService>();
        services.TryAddScoped<IOuroborosAgentService, OuroborosAgentService>();
        services.TryAddScoped<ISkillsService, SkillsService>();
        services.TryAddScoped<IOrchestratorService, OrchestratorService>();
        services.TryAddScoped<ICognitivePhysicsService, CognitivePhysicsService>();
        
        return services;
    }
    
    /// <summary>
    /// Registers command handlers
    /// </summary>
    public static IServiceCollection AddCommandHandlers(this IServiceCollection services)
    {
        // Register command handlers
        services.AddAskCommandHandler();
        // Add other command handlers here...
        
        return services;
    }
    
    /// <summary>
    /// Registers infrastructure services
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Spectre.Console service
        services.TryAddSingleton<ISpectreConsoleService, SpectreConsoleService>();
        
        // Voice integration service
        services.TryAddScoped<IVoiceIntegrationService, VoiceIntegrationService>();
        
        return services;
    }
    
    /// <summary>
    /// Registers Cognitive Physics Engine dependencies.
    /// IEmbeddingProvider and IEthicsGate are marked Obsolete in foundation but
    /// still required by CognitivePhysicsEngine's constructor.
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
    /// Registers existing business logic services
    /// </summary>
    public static IServiceCollection AddExistingBusinessLogic(this IServiceCollection services)
    {
        // Register existing services that are already in the codebase
        // This ensures we don't duplicate functionality
        
        // VoiceModeService (existing)
        services.TryAddScoped<VoiceModeService>();
        
        // Other existing services would be registered here
        
        return services;
    }
}

/// <summary>
/// No-op embedding provider used as a default when no real embedding model
/// is configured. Returns zero-vectors so CPE can still run (shift distances
/// will always be zero which means minimal resource cost).
/// </summary>
#pragma warning disable CS0618 // Obsolete IEmbeddingProvider — CPE requires it
file sealed class NullEmbeddingProvider : IEmbeddingProvider
{
    public ValueTask<float[]> GetEmbeddingAsync(string text) =>
        ValueTask.FromResult(new float[384]);
}
#pragma warning restore CS0618