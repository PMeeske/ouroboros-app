// <copyright file="OuroborosAgent.Tools.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Diagnostics;
using MediatR;
using Ouroboros.Abstractions.Monads;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Mediator;
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
                    var devicesResult = gateway.GetDevicesAsync().GetAwaiter().GetResult();
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
            ?? _config.Endpoint ?? "http://localhost:11434";
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

        // Register PTZ (Pan/Tilt/Zoom) control tool for motorized cameras
        // Uses ONVIF protocol via TapoCameraPtzClient for physical camera movement
        Ouroboros.Providers.Tapo.TapoCameraPtzClient? ptzClient = null;
        if (hasCredentials && tapoDevices.Count > 0)
        {
            var firstCamera = tapoDevices.First();
            ptzClient = new Ouroboros.Providers.Tapo.TapoCameraPtzClient(
                firstCamera.IpAddress, tapoUsername!, tapoPassword!);
        }

        var ptzRef = ptzClient;
        var ptzTool = new Ouroboros.Tools.DelegateTool(
            "camera_ptz",
            $"Control PTZ (Pan/Tilt/Zoom) motor on a Tapo camera. " +
            $"Use this when the user asks to pan, tilt, move, turn, rotate, or point the camera. " +
            $"Input: a command - one of: pan_left, pan_right, tilt_up, tilt_down, go_home, patrol, stop. " +
            $"Optionally append speed (0.1-1.0) after a space, e.g. 'pan_right 0.8'. Default speed: 0.5. " +
            $"Available cameras: {availableCameras}. Returns movement result.",
            async (string input, CancellationToken ct) =>
            {
                try
                {
                    if (ptzRef == null)
                    {
                        return Result<string, string>.Failure(
                            "PTZ not available. Tapo credentials missing or no camera devices configured. " +
                            "Set Tapo:Username, Tapo:Password, and Tapo:Devices in appsettings.json.");
                    }

                    // Initialize PTZ on first use
                    var initResult = await ptzRef.InitializeAsync(ct);
                    if (initResult.IsFailure)
                    {
                        return Result<string, string>.Failure($"PTZ init failed: {initResult.Error}");
                    }

                    // Parse command and optional speed
                    var parts = (input ?? "").Trim().ToLowerInvariant().Split(' ', 2);
                    var command = parts[0];
                    var speed = parts.Length > 1 && float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 0.5f;

                    var moveResult = command switch
                    {
                        "pan_left" or "left" => await ptzRef.PanLeftAsync(speed, ct: ct),
                        "pan_right" or "right" => await ptzRef.PanRightAsync(speed, ct: ct),
                        "tilt_up" or "up" => await ptzRef.TiltUpAsync(speed, ct: ct),
                        "tilt_down" or "down" => await ptzRef.TiltDownAsync(speed, ct: ct),
                        "stop" => await ptzRef.StopAsync(ct),
                        "go_home" or "home" or "center" => await ptzRef.GoToHomeAsync(ct),
                        "patrol" or "sweep" => await ptzRef.PatrolSweepAsync(speed, ct),
                        _ => Result<Ouroboros.Providers.Tapo.PtzMoveResult>.Failure(
                            $"Unknown PTZ command: '{command}'. Use: pan_left, pan_right, tilt_up, tilt_down, go_home, patrol, stop")
                    };

                    return moveResult.Match(
                        result => Result<string, string>.Success(
                            $"[PTZ] {result.Direction}: {result.Message} (duration: {result.Duration.TotalMilliseconds:F0}ms)"),
                        error => Result<string, string>.Failure(error));
                }
                catch (Exception ex)
                {
                    return Result<string, string>.Failure($"PTZ error: {ex.Message}");
                }
            });

        _tools = _tools.WithTool(ptzTool);
        _output.RecordInit("Camera", true, $"capture_camera + camera_ptz (cameras: {availableCameras})");

        // Register smart home actuator tool for lights, plugs, and other Tapo REST API devices
        var restClient = _tapoRestClient;
        var smartHomeTool = new Ouroboros.Tools.DelegateTool(
            "smart_home",
            "Control Tapo smart home devices (lights, plugs, color bulbs). " +
            "Use this when the user asks to turn on/off lights, plugs, switches, or set colors/brightness. " +
            "Input format: '<action> <device_name> [params]'. " +
            "Actions: turn_on, turn_off, set_brightness <0-100>, set_color <r> <g> <b>, list_devices, device_info. " +
            "Example: 'turn_on LivingRoomLight', 'set_color BedroomLight 255 0 128', 'set_brightness DeskLamp 75'. " +
            "Requires Tapo REST API server to be running.",
            async (string input, CancellationToken ct) =>
            {
                try
                {
                    if (restClient == null)
                    {
                        return Result<string, string>.Failure(
                            "Smart home control not available. Tapo REST API server address not configured in appsettings.json. " +
                            "Set Tapo:ServerAddress (e.g., 'http://localhost:8000') and ensure the tapo-rest server is running.");
                    }

                    var parts = (input ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 0)
                    {
                        return Result<string, string>.Failure(
                            "No action specified. Use: turn_on <device>, turn_off <device>, set_brightness <device> <0-100>, " +
                            "set_color <device> <r> <g> <b>, list_devices, device_info <device>");
                    }

                    var action = parts[0].ToLowerInvariant();

                    if (action == "list_devices")
                    {
                        var devicesResult = await restClient.GetDevicesAsync(ct);
                        return devicesResult.Match(
                            devices => Result<string, string>.Success(
                                devices.Count == 0
                                    ? "No devices found. Ensure the Tapo REST API server has devices configured."
                                    : $"Devices ({devices.Count}):\n" + string.Join("\n",
                                        devices.Select(d => $"  - {d.Name} ({d.DeviceType}) @ {d.IpAddress}"))),
                            error => Result<string, string>.Failure($"Failed to list devices: {error}"));
                    }

                    if (parts.Length < 2)
                    {
                        return Result<string, string>.Failure($"Device name required for action '{action}'");
                    }

                    var deviceName = parts[1];

                    switch (action)
                    {
                        case "turn_on":
                        {
                            // Try color bulb first, then regular bulb, then plug
                            var colorResult = await restClient.ColorLightBulbs.TurnOnAsync(deviceName, ct);
                            if (colorResult.IsSuccess) return Result<string, string>.Success($"Turned on {deviceName} (color light)");

                            var bulbResult = await restClient.LightBulbs.TurnOnAsync(deviceName, ct);
                            if (bulbResult.IsSuccess) return Result<string, string>.Success($"Turned on {deviceName} (light)");

                            var plugResult = await restClient.Plugs.TurnOnAsync(deviceName, ct);
                            if (plugResult.IsSuccess) return Result<string, string>.Success($"Turned on {deviceName} (plug)");

                            return Result<string, string>.Failure($"Could not turn on '{deviceName}'. Device may not exist or server may be unavailable.");
                        }

                        case "turn_off":
                        {
                            var colorResult = await restClient.ColorLightBulbs.TurnOffAsync(deviceName, ct);
                            if (colorResult.IsSuccess) return Result<string, string>.Success($"Turned off {deviceName} (color light)");

                            var bulbResult = await restClient.LightBulbs.TurnOffAsync(deviceName, ct);
                            if (bulbResult.IsSuccess) return Result<string, string>.Success($"Turned off {deviceName} (light)");

                            var plugResult = await restClient.Plugs.TurnOffAsync(deviceName, ct);
                            if (plugResult.IsSuccess) return Result<string, string>.Success($"Turned off {deviceName} (plug)");

                            return Result<string, string>.Failure($"Could not turn off '{deviceName}'. Device may not exist or server may be unavailable.");
                        }

                        case "set_brightness":
                        {
                            if (parts.Length < 3 || !byte.TryParse(parts[2], out var level) || level > 100)
                            {
                                return Result<string, string>.Failure("Brightness level required (0-100). Example: 'set_brightness DeskLamp 75'");
                            }

                            var colorResult = await restClient.ColorLightBulbs.SetBrightnessAsync(deviceName, level, ct);
                            if (colorResult.IsSuccess) return Result<string, string>.Success($"Set {deviceName} brightness to {level}%");

                            var bulbResult = await restClient.LightBulbs.SetBrightnessAsync(deviceName, level, ct);
                            if (bulbResult.IsSuccess) return Result<string, string>.Success($"Set {deviceName} brightness to {level}%");

                            return Result<string, string>.Failure($"Could not set brightness on '{deviceName}'. Device may not be a light.");
                        }

                        case "set_color":
                        {
                            if (parts.Length < 5 ||
                                !byte.TryParse(parts[2], out var r) ||
                                !byte.TryParse(parts[3], out var g) ||
                                !byte.TryParse(parts[4], out var b))
                            {
                                return Result<string, string>.Failure("RGB values required. Example: 'set_color BedroomLight 255 0 128'");
                            }

                            var color = new Ouroboros.Providers.Tapo.Color { Red = r, Green = g, Blue = b };
                            var result = await restClient.ColorLightBulbs.SetColorAsync(deviceName, color, ct);
                            return result.Match(
                                _ => Result<string, string>.Success($"Set {deviceName} color to RGB({r},{g},{b})"),
                                error => Result<string, string>.Failure($"Could not set color on '{deviceName}': {error}"));
                        }

                        case "device_info":
                        {
                            // Try each device type for info
                            var infoResult = await restClient.Plugs.GetDeviceInfoAsync(deviceName, ct);
                            if (infoResult.IsSuccess)
                                return Result<string, string>.Success($"Device info for {deviceName}:\n{infoResult.Value.RootElement}");

                            var lightInfo = await restClient.LightBulbs.GetDeviceInfoAsync(deviceName, ct);
                            if (lightInfo.IsSuccess)
                                return Result<string, string>.Success($"Device info for {deviceName}:\n{lightInfo.Value.RootElement}");

                            var colorInfo = await restClient.ColorLightBulbs.GetDeviceInfoAsync(deviceName, ct);
                            if (colorInfo.IsSuccess)
                                return Result<string, string>.Success($"Device info for {deviceName}:\n{colorInfo.Value.RootElement}");

                            return Result<string, string>.Failure($"Could not get info for '{deviceName}'.");
                        }

                        default:
                            return Result<string, string>.Failure(
                                $"Unknown action '{action}'. Use: turn_on, turn_off, set_brightness, set_color, list_devices, device_info");
                    }
                }
                catch (Exception ex)
                {
                    return Result<string, string>.Failure($"Smart home error: {ex.Message}");
                }
            });

        _tools = _tools.WithTool(smartHomeTool);
        _output.RecordInit("Smart Home", true, $"REST API: {(restClient != null ? tapoServerAddress : "not configured")}");
    }


    private Task<string> ListSkillsAsync()
        => _mediator.Send(new ListSkillsRequest());


    private Task<string> LearnTopicAsync(string topic)
        => _mediator.Send(new LearnTopicRequest(topic));

    internal static string SanitizeSkillName(string name)
    {
        return name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("'", "")
            .Replace("\"", "")
            .Replace("(", "")
            .Replace(")", "");
    }

    private Task<string> CreateToolAsync(string toolName)
        => _mediator.Send(new CreateToolRequest(toolName));

    private Task<string> UseToolAsync(string toolName, string? input)
        => _mediator.Send(new UseToolRequest(toolName, input));

    private Task<string> RunSkillAsync(string skillName)
        => _mediator.Send(new RunSkillRequest(skillName));

    private Task<string> SuggestSkillsAsync(string goal)
        => _mediator.Send(new SuggestSkillsRequest(goal));

    /// <summary>
    /// Registers Claude CLI-backed meta-tools for Iaret. Each tool shells out to the
    /// real <c>claude</c> executable (npm @anthropic-ai/claude-code), auto-discovered
    /// from PATH or the local VS Code extension bundle.
    ///   • claude_plan        — run <c>claude --print</c> to generate a structured plan
    ///   • claude_ask         — run <c>claude --print</c> to get a Claude answer
    ///   • claude_bypass_code — run <c>claude --dangerously-skip-permissions --print</c>
    /// </summary>
    private void RegisterClaudeStyleTools()
    {
        // ── claude_plan ──────────────────────────────────────────────────────
        var planTool = new DelegateTool(
            "claude_plan",
            "Use the Claude CLI to generate a detailed step-by-step plan before executing a " +
            "complex or multi-step task. Use this when a task requires several actions or " +
            "could have side-effects and you want a structured approach. " +
            "Input: describe the goal. " +
            "Returns the plan from Claude. The user can approve, revise, or cancel before you proceed.",
            async (string input, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(input))
                    return Result<string, string>.Failure("Input required: describe the goal to plan.");

                var prompt =
                    "Create a detailed, actionable step-by-step plan for the following task. " +
                    "Format as numbered steps with sub-steps where needed. Be specific.\n\nTask: " + input;

                var result = await RunClaudeAsync(["--print", prompt, "--output-format", "text"], ct);

                if (result.IsSuccess)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.Write(new Panel(Markup.Escape(result.Value))
                        .Header("[bold cyan]  Claude Plan  [/]")
                        .Border(BoxBorder.Rounded)
                        .BorderColor(Color.Cyan1));
                    AnsiConsole.MarkupLine(OuroborosTheme.Dim(
                        "  Say 'proceed' or 'approve' to execute, or give feedback to revise."));
                    AnsiConsole.WriteLine();
                }

                return result;
            });

        _tools = _tools.WithTool(planTool);

        // ── claude_ask ───────────────────────────────────────────────────────
        var askTool = new DelegateTool(
            "claude_ask",
            "Send a question or prompt to the Claude CLI and return its answer. " +
            "Use this when you need Claude's reasoning, knowledge, or a second opinion on something. " +
            "Input: the question or prompt to send. " +
            "Returns Claude's response.",
            async (string input, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(input))
                    return Result<string, string>.Failure("Input required: provide a question or prompt.");

                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Spectre.Console.Rule("[bold cyan]Claude CLI[/]").RuleStyle("cyan dim"));

                var result = await RunClaudeAsync(["--print", input, "--output-format", "text"], ct);

                AnsiConsole.WriteLine();
                return result;
            });

        _tools = _tools.WithTool(askTool);

        // ── claude_bypass_code ───────────────────────────────────────────────
        var bypassTool = new DelegateTool(
            "claude_bypass_code",
            "Run a task via the Claude CLI with --dangerously-skip-permissions, which allows " +
            "Claude to execute code, write files, and run shell commands without per-action approval. " +
            "Use this when you need Claude to autonomously complete a coding or file task. " +
            "Input: the task or prompt to execute with full permissions. " +
            "Returns Claude's output.",
            async (string input, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(input))
                    return Result<string, string>.Failure("Input required: provide the task for Claude to execute.");

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine(OuroborosTheme.Warn("  ⚡ Running Claude with --dangerously-skip-permissions…"));

                var result = await RunClaudeAsync(
                    ["--dangerously-skip-permissions", "--print", input, "--output-format", "text"], ct);

                AnsiConsole.WriteLine();
                return result;
            });

        _tools = _tools.WithTool(bypassTool);

        // ── claude_edit ──────────────────────────────────────────────────────
        var editTool = new DelegateTool(
            "claude_edit",
            "Use the Claude CLI to make targeted code edits to local files. " +
            "Claude is granted Read, Edit, Write, and Bash tool access so it can " +
            "read the file, apply the change, and verify the result — without full bypass. " +
            "Input: describe exactly what to change and in which file(s). " +
            "Returns Claude's edit summary. Call claude_continue afterwards to resume context.",
            async (string input, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(input))
                    return Result<string, string>.Failure("Input required: describe the edit to make.");

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine(OuroborosTheme.Dim("  ✏  Claude editing…"));

                var result = await RunClaudeAsync(
                    ["--allowedTools", "Read,Edit,Write,Bash", "--print", input, "--output-format", "text"], ct);

                AnsiConsole.WriteLine();
                return result;
            });

        _tools = _tools.WithTool(editTool);

        // ── claude_continue ──────────────────────────────────────────────────
        var continueTool = new DelegateTool(
            "claude_continue",
            "Resume the most recent Claude CLI conversation after local code changes, " +
            "so you do not have to exit and restart. " +
            "Use this after claude_edit or any manual file change to hand context back to Claude. " +
            "Input: optional follow-up message or leave empty to just continue. " +
            "Optionally prefix with a session ID and a space to resume a specific session: '<id> <message>'. " +
            "Returns Claude's continued response.",
            async (string input, CancellationToken ct) =>
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine(OuroborosTheme.Dim("  ↩  Resuming Claude session…"));

                // Allow optional "session-id message" prefix
                string? sessionId = null;
                var message = (input ?? string.Empty).Trim();
                var spaceIdx = message.IndexOf(' ');
                if (spaceIdx > 0)
                {
                    var candidate = message[..spaceIdx];
                    // Session IDs are long hex/UUID-ish strings — heuristic: no spaces, 8+ chars, no common words
                    if (candidate.Length >= 8 && !candidate.Contains('.') && !candidate.Contains('/'))
                    {
                        sessionId = candidate;
                        message = message[(spaceIdx + 1)..].Trim();
                    }
                }

                List<string> args;
                if (sessionId is not null)
                {
                    args = ["--resume", sessionId, "--print", message, "--output-format", "text"];
                }
                else if (string.IsNullOrWhiteSpace(message))
                {
                    args = ["--continue", "--print", "Please continue.", "--output-format", "text"];
                }
                else
                {
                    args = ["--continue", "--print", message, "--output-format", "text"];
                }

                var result = await RunClaudeAsync(args, ct);
                AnsiConsole.WriteLine();
                return result;
            });

        _tools = _tools.WithTool(continueTool);

        var claudePath = ResolveClaudeExecutable() ?? "claude (not found — install @anthropic-ai/claude-code)";
        _output.RecordInit("Claude CLI Tools", true,
            $"claude_plan + claude_ask + claude_bypass_code + claude_edit + claude_continue → {claudePath}");
    }

    // ── Claude CLI subprocess helpers ────────────────────────────────────────

    /// <summary>
    /// Invokes the <c>claude</c> executable with the given argument list.
    /// Tries PATH first, then the VS Code extension bundle.
    /// </summary>
    private static async Task<Result<string, string>> RunClaudeAsync(
        IReadOnlyList<string> args,
        CancellationToken ct = default)
    {
        var exe = ResolveClaudeExecutable();
        if (exe is null)
            return Result<string, string>.Failure(
                "claude CLI not found. Install with: npm install -g @anthropic-ai/claude-code");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var arg in args)
                psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi);
            if (process is null)
                return Result<string, string>.Failure("Failed to start claude process.");

            var stdout = await process.StandardOutput.ReadToEndAsync(ct);
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            return process.ExitCode == 0
                ? Result<string, string>.Success(stdout.Trim())
                : Result<string, string>.Failure(
                    string.IsNullOrWhiteSpace(stderr) ? $"claude exited with code {process.ExitCode}" : stderr.Trim());
        }
        catch (OperationCanceledException)
        {
            return Result<string, string>.Failure("Cancelled.");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"claude process error: {ex.Message}");
        }
    }

    /// <summary>
    /// Resolves the <c>claude</c> executable path.
    /// Prefers the system PATH entry; falls back to the VS Code extension bundle.
    /// </summary>
    private static string? ResolveClaudeExecutable()
    {
        // 1. Try PATH (works when installed via npm install -g)
        try
        {
            using var probe = Process.Start(new ProcessStartInfo
            {
                FileName = "claude",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            probe?.WaitForExit(2000);
            if (probe?.ExitCode == 0) return "claude";
        }
        catch { /* fall through */ }

        // 2. VS Code extension bundle (Windows: anthropic.claude-code-*)
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var extDir = Path.Combine(home, ".vscode", "extensions");
        if (Directory.Exists(extDir))
        {
            var claudeDir = Directory.GetDirectories(extDir, "anthropic.claude-code-*")
                .OrderByDescending(d => d)
                .FirstOrDefault();

            if (claudeDir is not null)
            {
                foreach (var candidate in new[] { "claude.exe", "claude" })
                {
                    var full = Path.Combine(claudeDir, "resources", "native-binary", candidate);
                    if (File.Exists(full)) return full;
                }
            }
        }

        return null;
    }
}
