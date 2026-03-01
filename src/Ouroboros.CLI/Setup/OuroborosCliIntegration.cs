// <copyright file="OuroborosCliIntegration.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>


namespace Ouroboros.CLI;

using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ouroboros.ApiHost.Extensions;
using Ouroboros.Application.Configuration;
using Ouroboros.Application.Integration;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Core.EmbodiedInteraction;
using Ouroboros.Providers;
using Ouroboros.Providers.Tapo;
using Spectre.Console;

/// <summary>
/// Integrates the full Ouroboros system into CLI commands.
/// Ensures all commands have access to the unified core by default.
/// </summary>
public static partial class OuroborosCliIntegration
{
    private static IServiceProvider? _serviceProvider;
    private static IOuroborosCore? _ouroborosCore;
    private static OuroborosTelemetry? _telemetry;
    private static TapoGatewayManager? _tapoGateway;

    /// <summary>
    /// Initializes the Ouroboros system for CLI usage.
    /// Should be called early in Program.cs startup.
    /// </summary>
    public static async Task<IServiceProvider> InitializeAsync(string[] args)
    {
        if (_serviceProvider != null)
        {
            return _serviceProvider;
        }

        var builder = Host.CreateDefaultBuilder(args);

        // Configure services
        builder.ConfigureServices((context, services) =>
        {
            // Load Ouroboros configuration
            var config = context.Configuration;
            var ouroborosConfig = OuroborosConfiguration.Load(config);

            // Register configuration
            services.AddSingleton(ouroborosConfig);

            // Register Qdrant + engine infrastructure (shared with all hosts)
            services.AddOuroborosEngine(config);

            // Add full Ouroboros system with monitoring
            services.AddOuroborosFullWithMonitoring();

            // Add Tapo embodiment if configured (lazy initialization - availability checked at runtime)
            var tapoServerAddress = config["Tapo:ServerAddress"];
            var tapoUsername = config["Tapo:Username"];
            var tapoPassword = config["Tapo:Password"];
            var tapoDevices = config.GetSection("Tapo:Devices").Get<List<TapoDevice>>();

            // Gateway mode: credentials exist but no explicit server address or devices
            // The gateway will auto-discover devices on the network
            var hasCredentials = !string.IsNullOrEmpty(tapoUsername) && !string.IsNullOrEmpty(tapoPassword);
            var useGateway = hasCredentials && string.IsNullOrEmpty(tapoServerAddress) && !(tapoDevices?.Count > 0);

            if (useGateway)
            {
                // Find the gateway script relative to the engine tools directory
                var gatewayPath = FindGatewayScriptPath();
                if (gatewayPath != null)
                {
                    services.AddTapoGateway(gatewayPath);
                    // REST client will be created after gateway starts (in TryInitializeTapoAsync)
                }
            }

            // Validate and register Tapo services based on configuration
            // Both RTSP cameras AND REST API can be used simultaneously
            var tapoConfigResult = ValidateTapoConfiguration(tapoServerAddress, tapoUsername, tapoPassword, tapoDevices);
            if (tapoConfigResult.IsValid || useGateway)
            {
                // Register RTSP cameras if configured (for direct camera access)
                if (tapoConfigResult.UseRtsp)
                {
                    services.AddTapoRtspCameras(tapoDevices!, tapoUsername!, tapoPassword!);
                }

                // Register REST API client if configured (for lights, plugs, etc.)
                // Availability is checked at runtime, not at registration
                if (tapoConfigResult.UseRestApi)
                {
                    services.AddTapoRestClient(tapoServerAddress!);
                }

                // Register unified embodiment provider that can use both
                services.AddSingleton<TapoEmbodimentProvider>(sp =>
                {
                    var restClient = sp.GetService<TapoRestClient>();
                    var rtspFactory = sp.GetService<ITapoRtspClientFactory>();
                    var visionModel = sp.GetService<IVisionModel>();
                    var ttsModel = sp.GetService<ITtsModel>();
                    var visionConfig = sp.GetService<TapoVisionModelConfig>() ?? TapoVisionModelConfig.CreateDefault();
                    var logger = sp.GetService<ILogger<TapoEmbodimentProvider>>();

                    return new TapoEmbodimentProvider(
                        restClient, rtspFactory, "tapo", visionModel, ttsModel, visionConfig, logger,
                        username: tapoUsername, password: tapoPassword);
                });

                services.AddSingleton<IEmbodimentProvider>(sp => sp.GetRequiredService<TapoEmbodimentProvider>());
            }

            // Add logging
            services.AddLogging();

            // Register Ollama vision model for embodiment and multi-model swarm
            var ollamaEndpoint = config["Ollama:Endpoint"] ?? DefaultEndpoints.Ollama;
            var visionModelName = config["Ollama:VisionModel"] ?? OllamaVisionModel.DefaultModel;
            services.AddSingleton<IVisionModel>(sp =>
            {
                var visionLogger = sp.GetService<ILogger<OllamaVisionModel>>();
                return new OllamaVisionModel(ollamaEndpoint, visionModelName, logger: visionLogger);
            });

            // Register VirtualSelf for avatar perception loop and embodied interaction
            services.AddSingleton<VirtualSelf>(sp => new VirtualSelf("Iaret"));
        });

        var host = builder.Build();
        _serviceProvider = host.Services;

        // Resolve core components
        _ouroborosCore = _serviceProvider.GetService<IOuroborosCore>();
        _telemetry = _serviceProvider.GetService<OuroborosTelemetry>();

        // Auto-authenticate Tapo if configured
        await TryInitializeTapoAsync(host.Services.GetRequiredService<IConfiguration>());

        return _serviceProvider;
    }

    /// <summary>
    /// Gets the Ouroboros core instance.
    /// Initializes system if not already done.
    /// </summary>
    public static IOuroborosCore? GetCore()
    {
        return _ouroborosCore;
    }

    /// <summary>
    /// Gets the telemetry instance.
    /// </summary>
    public static OuroborosTelemetry? GetTelemetry()
    {
        return _telemetry;
    }

    /// <summary>
    /// Gets the service provider for dependency resolution.
    /// </summary>
    public static IServiceProvider? GetServiceProvider()
    {
        return _serviceProvider;
    }

    /// <summary>
    /// Checks if Ouroboros system is initialized.
    /// </summary>
    public static bool IsInitialized => _serviceProvider != null;

    /// <summary>
    /// Ensures Ouroboros is initialized before command execution.
    /// Call this at the start of any CLI command that should integrate with Ouroboros.
    /// </summary>
    public static async Task EnsureInitializedAsync(string[] args)
    {
        if (!IsInitialized)
        {
            try
            {
                await InitializeAsync(args);
            }
            catch (InvalidOperationException ex)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Warn($"[WARN] Could not initialize Ouroboros system: {Markup.Escape(ex.Message)}"));
                AnsiConsole.MarkupLine(OuroborosTheme.Dim("[INFO] Commands will run in standalone mode"));
            }
        }
    }

    /// <summary>
    /// Broadcasts information to consciousness if available.
    /// Used to make the system aware of CLI activities.
    /// </summary>
    public static async Task BroadcastToConsciousnessAsync(string content, string source)
    {
        if (_ouroborosCore?.Consciousness != null)
        {
            await _ouroborosCore.Consciousness.BroadcastToConsciousnessAsync(
                content,
                source);
        }
    }
}
