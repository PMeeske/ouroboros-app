// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using Microsoft.Extensions.DependencyInjection;
using Ouroboros.CLI.Commands;

/// <summary>
/// Extension methods to register all agent subsystems in a DI container.
/// </summary>
public static class SubsystemRegistration
{
    /// <summary>
    /// Registers all agent subsystems as singletons in the service collection.
    /// Call <see cref="AddOuroborosAgent"/> after to register the agent itself.
    /// </summary>
    public static IServiceCollection AddAgentSubsystems(this IServiceCollection services, OuroborosConfig config)
    {
        // Configuration
        services.AddSingleton(config);

        // Build the voice service (required for agent construction)
        var voiceService = new VoiceModeService(new VoiceModeConfig(
            Persona: config.Persona,
            VoiceOnly: config.VoiceOnly,
            LocalTts: config.LocalTts,
            VoiceLoop: true,
            DisableStt: true,
            Model: config.Model,
            Endpoint: config.Endpoint,
            EmbedModel: config.EmbedModel,
            QdrantEndpoint: config.QdrantEndpoint,
            Culture: config.Culture));

        // Register subsystems
        services.AddSingleton<IVoiceSubsystem>(new VoiceSubsystem(voiceService));
        services.AddSingleton<IModelSubsystem, ModelSubsystem>();
        services.AddSingleton<IToolSubsystem, ToolSubsystem>();
        services.AddSingleton<IMemorySubsystem, MemorySubsystem>();
        services.AddSingleton<ICognitiveSubsystem, CognitiveSubsystem>();
        services.AddSingleton<IAutonomySubsystem, AutonomySubsystem>();
        services.AddSingleton<IEmbodimentSubsystem, EmbodimentSubsystem>();

        return services;
    }

    /// <summary>
    /// Registers the OuroborosAgent and wires it to all subsystems.
    /// </summary>
    public static IServiceCollection AddOuroborosAgent(this IServiceCollection services)
    {
        services.AddSingleton<OuroborosAgent>();
        return services;
    }

    /// <summary>
    /// Convenience method to register everything: subsystems + agent.
    /// </summary>
    public static IServiceCollection AddOuroboros(this IServiceCollection services, OuroborosConfig config)
    {
        return services
            .AddAgentSubsystems(config)
            .AddOuroborosAgent();
    }
}
