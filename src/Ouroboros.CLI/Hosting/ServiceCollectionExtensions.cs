using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Services;

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
        
        return services;
    }
    
    /// <summary>
    /// Registers command handlers
    /// </summary>
    public static IServiceCollection AddCommandHandlers(this IServiceCollection services)
    {
        // Command handlers will be registered here
        // These will wrap the existing business logic
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