// <copyright file="AgentBootstrapper.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Application.Tools;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Subsystems;
using Ouroboros.Options;

namespace Ouroboros.CLI.Setup;

/// <summary>
/// Shared bootstrapping logic for creating OuroborosAgent instances
/// from various CLI option types. Eliminates duplication between
/// the 'ouroboros' and 'assist' command verbs.
/// </summary>
public static class AgentBootstrapper
{
    /// <summary>
    /// Loads and configures the IConfiguration from standard sources.
    /// </summary>
    /// <returns>The loaded configuration.</returns>
    public static IConfiguration LoadConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddUserSecrets<OuroborosAgent>(optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    /// <summary>
    /// Applies configuration to all static providers (ApiKeyProvider, ChatConfig, etc.).
    /// </summary>
    /// <param name="configuration">The configuration to apply.</param>
    public static void ApplyConfiguration(IConfiguration configuration)
    {
        // Set configuration for API key provider (used by Firecrawl, etc.)
        ApiKeyProvider.SetConfiguration(configuration);

        // Set configuration for ChatConfig (used for Anthropic, GitHub Models, etc.)
        ChatConfig.SetConfiguration(configuration);

        // Set configuration for Azure Speech TTS
        OuroborosAgent.SetConfiguration(configuration);
        VoiceModeService.SetConfiguration(configuration);
    }

    /// <summary>
    /// Creates an OuroborosConfig from OuroborosOptions (full-featured path).
    /// </summary>
    /// <param name="opts">The Ouroboros options.</param>
    /// <returns>The configured OuroborosConfig.</returns>
    public static OuroborosConfig CreateConfig(OuroborosOptions opts)
    {
        // Determine if Azure TTS should be used (prefer Azure if credentials available, allow override with --local-tts)
        var azureKey = opts.AzureSpeechKey ?? Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
        var useAzureTts = opts.LocalTts ? false : (opts.AzureTts && !string.IsNullOrEmpty(azureKey));

        // Create unified config from CLI options - ALL features enabled by default
        return new OuroborosConfig(
            Persona: opts.Persona,
            Model: opts.Model,
            Endpoint: opts.Endpoint,
            EmbedModel: opts.EmbedModel,
            EmbedEndpoint: opts.EmbedEndpoint,
            QdrantEndpoint: opts.QdrantEndpoint,
            ApiKey: opts.ApiKey ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"),
            EndpointType: opts.EndpointType,
            // Voice is disabled in push/yolo mode by default unless --push-voice is used
            Voice: (opts.Push || opts.Yolo) ? opts.PushVoice : (opts.Voice && !opts.TextOnly),
            VoiceOnly: opts.VoiceOnly,
            LocalTts: opts.LocalTts,
            AzureTts: useAzureTts,
            AzureSpeechKey: azureKey,
            AzureSpeechRegion: opts.AzureSpeechRegion,
            TtsVoice: opts.TtsVoice,
            VoiceChannel: opts.VoiceChannel,
            VoiceV2: opts.VoiceV2,
            Listen: opts.Listen,
            Debug: opts.Debug,
            Temperature: opts.Temperature,
            MaxTokens: opts.MaxTokens,
            Culture: opts.Culture,
            // Feature toggles - invert the "No" flags
            EnableSkills: !opts.NoSkills,
            EnableMeTTa: !opts.NoMeTTa,
            EnableTools: !opts.NoTools,
            EnablePersonality: !opts.NoPersonality,
            EnableMind: !opts.NoMind,
            EnableBrowser: !opts.NoBrowser,
            // Autonomous/Push mode
            EnablePush: opts.Push,
            YoloMode: opts.Yolo,
            AutoApproveCategories: opts.AutoApprove,
            IntentionIntervalSeconds: opts.IntentionInterval,
            DiscoveryIntervalSeconds: opts.DiscoveryInterval,
            // Governance & Self-Modification
            EnableSelfModification: opts.EnableSelfModification,
            RiskLevel: opts.RiskLevel,
            AutoApproveLow: opts.AutoApproveLow,
            // Additional config
            ThinkingIntervalSeconds: opts.ThinkingInterval,
            AgentMaxSteps: opts.AgentMaxSteps,
            InitialGoal: opts.Goal,
            InitialQuestion: opts.Question,
            InitialDsl: opts.Dsl,
            // Multi-model
            CoderModel: opts.CoderModel,
            ReasonModel: opts.ReasonModel,
            SummarizeModel: opts.SummarizeModel,
            VisionModel: opts.VisionModel,
            // Piping & Batch mode
            PipeMode: opts.Pipe,
            BatchFile: opts.BatchFile,
            JsonOutput: opts.JsonOutput,
            NoGreeting: opts.NoGreeting || opts.Pipe || !string.IsNullOrWhiteSpace(opts.BatchFile) || !string.IsNullOrWhiteSpace(opts.Exec),
            ExitOnError: opts.ExitOnError,
            ExecCommand: opts.Exec,
            // Cost tracking & efficiency
            ShowCosts: opts.ShowCosts,
            CostAware: opts.CostAware,
            CostSummary: opts.CostSummary,
            // Collective Mind (Multi-Provider)
            CollectiveMode: opts.CollectiveMode,
            CollectivePreset: opts.CollectivePreset,
            CollectiveThinkingMode: opts.CollectiveThinkingMode,
            CollectiveProviders: opts.CollectiveProviders,
            Failover: opts.Failover,
            // Election & Orchestration
            ElectionStrategy: opts.ElectionStrategy,
            MasterModel: opts.MasterModel,
            EvaluationCriteria: opts.EvaluationCriteria,
            ShowElection: opts.ShowElection,
            ShowOptimization: opts.ShowOptimization
        );
    }

    /// <summary>
    /// Creates an OuroborosConfig from AssistOptions (legacy/compat path).
    /// </summary>
    /// <param name="opts">The assist options.</param>
    /// <returns>The configured OuroborosConfig.</returns>
    public static OuroborosConfig CreateConfig(AssistOptions opts)
    {
        // DEPRECATED: AssistOptions is maintained for backward compatibility only.
        // Use OuroborosOptions for full features including autonomous mode, multi-model, etc.
        return new OuroborosConfig(
            Persona: opts.Persona,
            Model: opts.Model,
            Endpoint: opts.Endpoint ?? "http://localhost:11434",
            EmbedModel: opts.EmbedModel,
            EmbedEndpoint: "http://localhost:11434",
            QdrantEndpoint: opts.QdrantEndpoint,
            ApiKey: Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"),
            Voice: opts.Voice,
            VoiceOnly: opts.VoiceOnly,
            LocalTts: opts.LocalTts,
            VoiceChannel: opts.VoiceChannel,
            Debug: opts.Debug,
            Temperature: opts.Temperature,
            MaxTokens: opts.MaxTokens,
            Culture: opts.Culture,
            InitialGoal: opts.Goal,
            InitialDsl: opts.Dsl
        );
    }

    /// <summary>
    /// Creates and initializes an OuroborosAgent from config.
    /// </summary>
    /// <param name="config">The configuration to use.</param>
    /// <returns>The initialized agent.</returns>
    public static async Task<OuroborosAgent> CreateAgentAsync(OuroborosConfig config)
    {
        var agent = new OuroborosAgent(config);
        await agent.InitializeAsync();
        return agent;
    }

    /// <summary>
    /// Creates and initializes an OuroborosAgent via full DI container.
    /// Subsystems are resolved from the container and injected into the agent.
    /// </summary>
    /// <param name="config">The configuration to use.</param>
    /// <returns>The initialized agent (dispose via the returned ServiceProvider).</returns>
    public static async Task<(OuroborosAgent Agent, ServiceProvider Provider)> CreateAgentWithDIAsync(OuroborosConfig config)
    {
        var services = new ServiceCollection();
        services.AddOuroboros(config);

        var provider = services.BuildServiceProvider();
        var agent = provider.GetRequiredService<OuroborosAgent>();
        await agent.InitializeAsync();

        return (agent, provider);
    }
}
