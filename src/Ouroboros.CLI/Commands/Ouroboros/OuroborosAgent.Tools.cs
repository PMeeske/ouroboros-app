// <copyright file="OuroborosAgent.Tools.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Abstractions.Monads;
using Ouroboros.Application.Configuration;
using Ouroboros.CLI.Avatar;
using Spectre.Console;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

namespace Ouroboros.CLI.Commands;

public sealed partial class OuroborosAgent
{
    /// <summary>
    /// Adds a tool to the registry and refreshes the LLM to use the updated tools.
    /// This ensures dynamically created tools are immediately available for use.
    /// </summary>
    /// <param name="tool">The tool to add.</param>
    internal void AddToolAndRefreshLlm(ITool tool)
    {
        _tools = _tools.WithTool(tool);

        // Recreate ToolAwareChatModel with updated tools
        // Use orchestrated model (swarm) when available for automatic vision/coder/reasoner routing
        var effectiveModel = GetEffectiveChatModel();
        if (effectiveModel != null)
        {
            _llm = new ToolAwareChatModel(effectiveModel, _tools);
            System.Diagnostics.Debug.WriteLine($"[Tools] Refreshed _llm with {_tools.Count} tools after adding {tool.Name}");
        }

        // Also update the smart tool selector if available
        if (_smartToolSelector != null && _worldState != null && _toolCapabilityMatcher != null)
        {
            _toolCapabilityMatcher = new ToolCapabilityMatcher(_tools);
            _smartToolSelector = new SmartToolSelector(
                _worldState,
                _tools,
                _toolCapabilityMatcher,
                _smartToolSelector.Configuration);
        }
    }

    /// <summary>
    /// Gets the best available chat model for tool-aware wrapping.
    /// Prefers the orchestrated model (swarm router) when available,
    /// so vision/coder/reasoner keywords route to specialized sub-models.
    /// </summary>
    private IChatCompletionModel? GetEffectiveChatModel()
        => (IChatCompletionModel?)_orchestratedModel ?? _chatModel;

    /// <summary>
    /// Registers the capture_camera tool from Tapo config.
    /// Reads camera devices from _staticConfiguration and creates an RTSP-backed tool.
    /// If config is missing or incomplete, registers a stub tool that returns an honest error.
    /// </summary>
    private void RegisterCameraCaptureTool()
    {
        // Read Tapo camera config
        var tapoDeviceSection = _staticConfiguration?.GetSection("Tapo:Devices");
        var tapoDeviceConfigs = tapoDeviceSection?.GetChildren().ToList();
        var tapoUsername = _staticConfiguration?["Tapo:Username"];
        var tapoPassword = _staticConfiguration?["Tapo:Password"];

        // Build camera name list from config (or default)
        var cameraNames = new List<string>();
        var tapoDevices = new List<Ouroboros.Providers.Tapo.TapoDevice>();

        if (tapoDeviceConfigs != null && tapoDeviceConfigs.Count > 0)
        {
            tapoDevices = tapoDeviceConfigs
                .Select(d => new Ouroboros.Providers.Tapo.TapoDevice
                {
                    Name = d["name"] ?? d["ip_addr"] ?? "unknown",
                    IpAddress = d["ip_addr"] ?? "unknown",
                    DeviceType = Enum.TryParse<Ouroboros.Providers.Tapo.TapoDeviceType>(
                        d["device_type"], true, out var dt)
                        ? dt
                        : Ouroboros.Providers.Tapo.TapoDeviceType.C200,
                })
                .Where(d => IsCameraDeviceType(d.DeviceType))
                .ToList();

            cameraNames = tapoDevices.Select(d => d.Name).ToList();
        }

        // Create RTSP factory if we have both devices and credentials
        var hasCredentials = !string.IsNullOrEmpty(tapoUsername) && !string.IsNullOrEmpty(tapoPassword);
        if (hasCredentials && tapoDevices.Count > 0)
        {
            _tapoRtspFactory = new Ouroboros.Providers.Tapo.TapoRtspClientFactory(
                tapoDevices, tapoUsername!, tapoPassword!);
        }

        // Create REST client for smart home actuators (lights, plugs) if server address is configured
        var tapoServerAddress = _staticConfiguration?["Tapo:ServerAddress"];
        if (!string.IsNullOrEmpty(tapoServerAddress))
        {
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(tapoServerAddress),
                Timeout = TimeSpan.FromSeconds(30)
            };
            _tapoRestClient = new Ouroboros.Providers.Tapo.TapoRestClient(httpClient);
        }

        // Fallback: use gateway-discovered devices if no static config and gateway is running
        if (tapoDevices.Count == 0 && _tapoRestClient == null)
        {
            var gateway = _embodimentSub?.TapoRestClient;
            if (gateway != null)
            {
                _tapoRestClient = gateway;
                // Try to fetch discovered camera devices from the gateway
                try
                {
                    var devicesResult = Task.Run(async () => await gateway.GetDevicesAsync()).GetAwaiter().GetResult(); // sync-over-async:accepted — sync init path
                    if (devicesResult.IsSuccess)
                    {
                        var discoveredCameras = devicesResult.Value
                            .Where(d => IsCameraDeviceType(d.DeviceType))
                            .ToList();
                        if (hasCredentials && discoveredCameras.Count > 0)
                        {
                            _tapoRtspFactory = new Ouroboros.Providers.Tapo.TapoRtspClientFactory(
                                discoveredCameras, tapoUsername!, tapoPassword!);
                            cameraNames = discoveredCameras.Select(d => d.Name).ToList();
                        }
                    }
                }
                catch
                {
                    // Gateway not ready yet - cameras will be unavailable
                }
            }
        }

        // Create vision model from config
        var ollamaEndpoint = _staticConfiguration?["Ollama:Endpoint"]
            ?? _config.Endpoint ?? DefaultEndpoints.Ollama;
        var visionModelName = _staticConfiguration?["Ollama:VisionModel"]
            ?? Ouroboros.Providers.OllamaVisionModel.DefaultModel;
        _visionModel = new Ouroboros.Providers.OllamaVisionModel(ollamaEndpoint, visionModelName);

        // Capture closures for the lambda
        var defaultCamera = cameraNames.Count > 0 ? cameraNames.First() : "Camera1";
        var rtspFactory = _tapoRtspFactory;
        var visionModel = _visionModel;
        var availableCameras = cameraNames.Count > 0
            ? string.Join(", ", cameraNames) : "none configured";

        var captureTool = new Ouroboros.Tools.DelegateTool(
            "capture_camera",
            $"Capture a live frame from a Tapo RTSP camera and analyze it with vision AI. " +
            $"YOU MUST use this tool when the user asks to see, look, or check the camera. " +
            $"Input: camera name (available: {availableCameras}, default: {defaultCamera}). " +
            $"Returns a real description of what the camera sees. NEVER make up or hallucinate camera output.",
            async (string input, CancellationToken ct) =>
            {
                try
                {
                    if (rtspFactory == null)
                    {
                        return Result<string, string>.Failure(
                            "Camera not available. Tapo RTSP credentials missing or no camera devices configured in appsettings.json. " +
                            "Set Tapo:Username, Tapo:Password, and Tapo:Devices to enable camera access.");
                    }

                    var cameraName = string.IsNullOrWhiteSpace(input)
                        ? defaultCamera : input.Trim();
                    var client = rtspFactory.GetClient(cameraName);
                    if (client == null)
                    {
                        return Result<string, string>.Failure(
                            $"Camera '{cameraName}' not found. Available: {availableCameras}");
                    }

                    // Capture frame via RTSP/FFmpeg
                    var frameResult = await client.CaptureFrameAsync(ct);
                    if (frameResult.IsFailure)
                    {
                        return Result<string, string>.Failure(
                            $"Frame capture failed: {frameResult.Error}");
                    }

                    var frame = frameResult.Value;

                    // Analyze with vision model
                    var options = new Ouroboros.Core.EmbodiedInteraction.VisionAnalysisOptions();
                    var analysisResult = await visionModel.AnalyzeImageAsync(
                        frame.Data, "jpeg", options, ct);

                    return analysisResult.Match(
                        analysis => Result<string, string>.Success(
                            $"[Camera: {cameraName} | {frame.Width}x{frame.Height} | Frame #{frame.FrameNumber} | {frame.Timestamp:HH:mm:ss}]\n" +
                            $"Description: {analysis.Description}" +
                            (analysis.SceneType != null ? $"\nScene: {analysis.SceneType}" : "") +
                            (analysis.Objects.Count > 0
                                ? $"\nObjects: {string.Join(", ", analysis.Objects.Select(o => $"{o.Label} ({o.Confidence:P0})"))}"
                                : "") +
                            (analysis.Faces.Count > 0
                                ? $"\nFaces: {analysis.Faces.Count} detected"
                                : "") +
                            $"\nConfidence: {analysis.Confidence:P0} | Processing: {analysis.ProcessingTimeMs}ms"),
                        error => Result<string, string>.Failure(
                            $"Vision analysis failed: {error}"));
                }
                catch (Exception ex)
                {
                    return Result<string, string>.Failure(
                        $"Camera capture error: {ex.Message}");
                }
            });

        _tools = _tools.WithTool(captureTool);

        // Delegate PTZ and smart home tool registration to the SmartHome partial
        RegisterSmartHomeTools(hasCredentials, tapoUsername, tapoPassword, tapoDevices, availableCameras, tapoServerAddress);
    }
}
