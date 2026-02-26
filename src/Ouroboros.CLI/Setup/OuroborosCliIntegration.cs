// <copyright file="OuroborosCliIntegration.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Ouroboros.CLI;

using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ouroboros.ApiHost.Extensions;
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
public static class OuroborosCliIntegration
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
            var ollamaEndpoint = config["Ollama:Endpoint"] ?? "http://localhost:11434";
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
    /// Initializes Tapo embodiment - either via REST API authentication or RTSP connection.
    /// REST API availability is checked before attempting connection.
    /// </summary>
    private static async Task TryInitializeTapoAsync(IConfiguration config)
    {
        // Try starting the gateway if registered
        var gateway = _serviceProvider?.GetService<TapoGatewayManager>();
        if (gateway != null)
        {
            await TryStartGatewayAsync(gateway, config);
        }

        var tapoProvider = _serviceProvider?.GetService<TapoEmbodimentProvider>();
        if (tapoProvider == null)
        {
            return; // Tapo not configured
        }

        // Initialize both RTSP cameras AND REST API devices (both can be active simultaneously)
        var hasRtsp = tapoProvider.RtspClientFactory != null;
        var hasRestApi = tapoProvider.RestClient != null;

        if (!hasRtsp && !hasRestApi)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Dim("[Tapo] No RTSP cameras or REST API configured"));
            return;
        }

        // First initialize RTSP cameras (direct camera access)
        if (hasRtsp)
        {
            await InitializeRtspCamerasAsync(tapoProvider);
        }

        // Then initialize REST API (smart plugs/lights control)
        if (hasRestApi)
        {
            await InitializeRestApiAsync(tapoProvider, config);
        }

        // Now connect the provider (initializes both RTSP and REST if configured)
        if (!tapoProvider.IsConnected)
        {
            var connectResult = await tapoProvider.ConnectAsync();
            if (connectResult.IsFailure)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Warn($"[Tapo] Connection failed: {Markup.Escape(connectResult.Error.ToString())}"));
            }
            else
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Dim("[Tapo] Embodiment provider connected successfully"));
            }
        }
    }

    /// <summary>
    /// Starts the Tapo Gateway process with auto-discovery.
    /// </summary>
    private static async Task TryStartGatewayAsync(TapoGatewayManager gateway, IConfiguration config)
    {
        var username = config["Tapo:Username"];
        var password = config["Tapo:Password"];
        var serverPassword = config["Tapo:ServerPassword"] ?? "ouroboros-gateway";

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            return;

        AnsiConsole.MarkupLine(OuroborosTheme.Dim("[Tapo] Starting Tapo Gateway with auto-discovery..."));

        var started = await gateway.StartAsync(username, password, serverPassword, port: 8123);
        if (!started)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn("[Tapo] Gateway failed to start (Python 3 + tapo + fastapi + uvicorn required)"));
            return;
        }

        _tapoGateway = gateway;
        AnsiConsole.MarkupLine(OuroborosTheme.Dim($"[Tapo] Gateway running on port {gateway.Port}"));

        // Create REST client pointing to the gateway and inject it into the provider
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(gateway.BaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
        var restClient = new TapoRestClient(httpClient);

        // Authenticate with the gateway
        var authResult = await restClient.LoginAsync(serverPassword);
        if (authResult.IsFailure)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn($"[Tapo] Gateway auth failed: {Markup.Escape(authResult.Error.ToString())}"));
            return;
        }

        // Log discovered devices
        var devicesResult = await restClient.GetDevicesAsync();
        if (devicesResult.IsSuccess)
        {
            var devices = devicesResult.Value;
            AnsiConsole.MarkupLine(OuroborosTheme.Dim($"[Tapo] Gateway discovered {devices.Count} device(s):"));
            foreach (var device in devices)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Dim($"[Tapo]   - {Markup.Escape(device.Name)} ({device.DeviceType}) @ {Markup.Escape(device.IpAddress)}"));
            }
        }

        // Wire the REST client into the embodiment provider if registered
        var tapoProvider = _serviceProvider?.GetService<TapoEmbodimentProvider>();
        if (tapoProvider != null)
        {
            tapoProvider.SetRestClient(restClient);
        }
    }

    /// <summary>
    /// Initializes RTSP cameras and displays available cameras.
    /// </summary>
    private static Task InitializeRtspCamerasAsync(TapoEmbodimentProvider tapoProvider)
    {
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("[Tapo] RTSP camera support enabled"));
        var cameraNames = tapoProvider.RtspClientFactory!.GetCameraNames().ToList();
        AnsiConsole.MarkupLine(OuroborosTheme.Dim($"[Tapo] {cameraNames.Count} camera(s) registered:"));

        // Display each camera (non-blocking, just log)
        foreach (var cameraName in cameraNames)
        {
            var rtspClient = tapoProvider.RtspClientFactory.GetClient(cameraName);
            if (rtspClient != null)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Dim($"[Tapo]   - {Markup.Escape(cameraName)} @ {Markup.Escape(rtspClient.CameraIp)}"));
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Initializes REST API connection with availability check.
    /// </summary>
    private static async Task InitializeRestApiAsync(TapoEmbodimentProvider tapoProvider, IConfiguration config)
    {
        var serverPassword = config["Tapo:ServerPassword"];
        var serverAddress = config["Tapo:ServerAddress"];

        if (string.IsNullOrEmpty(serverPassword))
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Dim("[Tapo] REST API configured but no server password set (Tapo:ServerPassword)"));
            return;
        }

        // Check if REST API server is reachable before attempting authentication
        var isAvailable = await CheckRestApiAvailabilityAsync(serverAddress);
        if (!isAvailable)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Dim($"[Tapo] REST API server not available at {Markup.Escape(serverAddress ?? "")} (this is optional)"));
            return;
        }

        var authResult = await tapoProvider.AuthenticateAsync(serverPassword);
        if (authResult.IsSuccess)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Dim("[Tapo] Connected to Tapo REST API"));
        }
        else
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn($"[Tapo] REST API authentication failed: {Markup.Escape(authResult.Error.ToString())}"));
        }
    }

    /// <summary>
    /// Checks if the Tapo REST API server is reachable.
    /// </summary>
    private static async Task<bool> CheckRestApiAvailabilityAsync(string? serverAddress)
    {
        if (string.IsNullOrEmpty(serverAddress))
            return false;

        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var response = await httpClient.GetAsync(serverAddress);
            // Any response means the server is running (even 401/403)
            return true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false; // Timeout
        }
        catch (Exception)
        {
            return false;
        }
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
    /// Result of Tapo configuration validation.
    /// </summary>
    private sealed record TapoConfigValidationResult(
        bool IsValid,
        bool UseRtsp,
        bool UseRestApi,
        string? ValidationMessage);

    /// <summary>
    /// Validates Tapo configuration and determines which mode to use.
    /// </summary>
    private static TapoConfigValidationResult ValidateTapoConfiguration(
        string? serverAddress,
        string? username,
        string? password,
        List<TapoDevice>? devices)
    {
        // Check for RTSP mode (direct camera access)
        var hasCredentials = !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password);
        var hasDevices = devices?.Count > 0;
        var hasCameraDevices = devices?.Any(d => IsCameraDevice(d.DeviceType)) ?? false;

        if (hasCredentials && hasDevices && hasCameraDevices)
        {
            // Validate device configurations
            foreach (var device in devices!)
            {
                if (string.IsNullOrWhiteSpace(device.Name))
                {
                    return new TapoConfigValidationResult(false, false, false,
                        "Device name is required for all Tapo devices");
                }

                if (string.IsNullOrWhiteSpace(device.IpAddress) || device.IpAddress == "192.168.1.1")
                {
                    AnsiConsole.MarkupLine(OuroborosTheme.Warn($"[Tapo] Warning: Device '{Markup.Escape(device.Name)}' has placeholder IP address. Update appsettings.json with actual camera IP."));
                }
            }

            // Enable REST API alongside RTSP if server address is configured and valid
            var useRestApi = !string.IsNullOrEmpty(serverAddress)
                && Uri.TryCreate(serverAddress, UriKind.Absolute, out var rtspUri)
                && (rtspUri.Scheme == "http" || rtspUri.Scheme == "https");

            return new TapoConfigValidationResult(true, true, useRestApi, null);
        }

        // Check for REST API mode (no cameras, just smart home devices)
        if (!string.IsNullOrEmpty(serverAddress))
        {
            // Validate URL format
            if (!Uri.TryCreate(serverAddress, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                return new TapoConfigValidationResult(false, false, false,
                    $"Invalid Tapo server address: {serverAddress}");
            }

            return new TapoConfigValidationResult(true, false, true, null);
        }

        // No Tapo configuration
        return new TapoConfigValidationResult(false, false, false, null);
    }

    /// <summary>
    /// Finds the tapo_gateway.py script by searching known locations.
    /// </summary>
    private static string? FindGatewayScriptPath()
    {
        // Search relative to the application base directory and common repo layouts
        var candidates = new[]
        {
            // When running from ouroboros-app with libs/engine submodule
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "libs", "engine", "tools", "tapo_gateway.py"),
            // When running from meta-repo (Ouroboros-v2)
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "engine", "tools", "tapo_gateway.py"),
            // When running from ouroboros-engine directly
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "tools", "tapo_gateway.py"),
            // Standalone peer checkout
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ouroboros-engine", "tools", "tapo_gateway.py"),
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }

    /// <summary>
    /// Checks if a device type is a camera.
    /// </summary>
    private static bool IsCameraDevice(TapoDeviceType deviceType) =>
        deviceType is TapoDeviceType.C100 or TapoDeviceType.C200 or TapoDeviceType.C210
            or TapoDeviceType.C220 or TapoDeviceType.C310 or TapoDeviceType.C320
            or TapoDeviceType.C420 or TapoDeviceType.C500 or TapoDeviceType.C520;

    /// <summary>
    /// Executes a goal using the Ouroboros system if initialized.
    /// Falls back gracefully if system is not initialized.
    /// </summary>
    public static async Task<bool> TryExecuteGoalAsync(
        string goal,
        ExecutionConfig? config = null,
        Action<PlanExecutionResult>? onSuccess = null,
        Action<string>? onError = null)
    {
        if (_ouroborosCore == null)
        {
            return false; // Not initialized, fall back to regular command handling
        }

        config ??= ExecutionConfig.Default;
        _telemetry?.RecordGoalExecution(true, TimeSpan.Zero);

        var result = await _ouroborosCore.ExecuteGoalAsync(goal, config);

        result.Match(
            success =>
            {
                _telemetry?.RecordGoalExecution(true, success.Duration);
                onSuccess?.Invoke(success);
            },
            error =>
            {
                _telemetry?.RecordError("goal_execution", "execution_failed");
                onError?.Invoke(error);
            });

        return true;
    }

    /// <summary>
    /// Performs reasoning using the Ouroboros system if initialized.
    /// </summary>
    public static async Task<bool> TryReasonAsync(
        string query,
        ReasoningConfig? config = null,
        Action<ReasoningResult>? onSuccess = null,
        Action<string>? onError = null)
    {
        if (_ouroborosCore == null)
        {
            return false;
        }

        config ??= ReasoningConfig.Default;
        var startTime = DateTime.UtcNow;

        var result = await _ouroborosCore.ReasonAboutAsync(query, config);

        var duration = DateTime.UtcNow - startTime;
        _telemetry?.RecordReasoningQuery(
            duration,
            config.UseSymbolicReasoning,
            config.UseCausalInference,
            config.UseAbduction);

        result.Match(
            onSuccess ?? (_ => { }),
            onError ?? (_ => { }));

        return true;
    }

    /// <summary>
    /// Records telemetry for CLI operations.
    /// </summary>
    public static void RecordCliOperation(string operation, bool success, TimeSpan duration)
    {
        _telemetry?.RecordGoalExecution(success, duration, new Dictionary<string, object>
        {
            ["operation"] = operation,
            ["cli"] = true
        });
    }

    /// <summary>
    /// Gets health status of the Ouroboros system.
    /// </summary>
    public static string GetHealthStatus()
    {
        if (_ouroborosCore == null)
        {
            return "Not initialized";
        }

        var status = new System.Text.StringBuilder();
        status.AppendLine("Ouroboros System Status:");
        status.AppendLine($"  Episodic Memory: {(_ouroborosCore.EpisodicMemory != null ? "✓" : "✗")}");
        status.AppendLine($"  MeTTa Reasoning: {(_ouroborosCore.MeTTaReasoning != null ? "✓" : "✗")}");
        status.AppendLine($"  Hierarchical Planner: {(_ouroborosCore.HierarchicalPlanner != null ? "✓" : "✗")}");
        status.AppendLine($"  Causal Reasoning: {(_ouroborosCore.CausalReasoning != null ? "✓" : "✗")}");
        status.AppendLine($"  Consciousness: {(_ouroborosCore.Consciousness != null ? "✓" : "✗")}");
        status.AppendLine($"  Reflection: {(_ouroborosCore.Reflection != null ? "✓" : "✗")}");

        return status.ToString();
    }

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
            catch (Exception ex)
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
