// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using Ouroboros.Application.Services;
using Ouroboros.Application.Tools;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Core.EmbodiedInteraction;
using Spectre.Console;

/// <summary>
/// Embodiment subsystem implementation owning physical sensors, actuators, and presence detection.
/// </summary>
public sealed class EmbodimentSubsystem : IEmbodimentSubsystem
{
    public string Name => "Embodiment";
    public bool IsInitialized { get; private set; }

    // Embodied Interaction
    public EmbodimentController? Controller { get; set; }
    public VirtualSelf? VirtualSelf { get; set; }
    public BodySchema? BodySchema { get; set; }

    // IoT / Tapo devices
    public Ouroboros.Providers.Tapo.ITapoRtspClientFactory? TapoRtspFactory { get; set; }
    public Ouroboros.Providers.Tapo.TapoRestClient? TapoRestClient { get; set; }

    // Presence Detection
    public PresenceDetector? PresenceDetector { get; set; }
    public AgiWarmup? AgiWarmup { get; set; }
    public bool UserWasPresent { get; set; } = true; // Assume present at startup
    public DateTime LastGreetingTime { get; set; } = DateTime.MinValue;

    // Interactive avatar
    public Application.Avatar.InteractiveAvatarService? AvatarService { get; set; }

    // Avatar video stream (live SD generation)
    public Application.Avatar.AvatarVideoStream? AvatarVideoStream { get; set; }

    // Live vision stream (Ollama VLM streaming analysis of avatar assets)
    public Avatar.LiveVisionStream? LiveVisionStream { get; set; }

    // Vision
    public VisionService? VisionService { get; set; }

    // Cross-subsystem context (set during InitializeAsync)
    internal SubsystemInitContext Ctx { get; private set; } = null!;

    public void MarkInitialized() => IsInitialized = true;

    /// <inheritdoc/>
    public async Task InitializeAsync(SubsystemInitContext ctx)
    {
        Ctx = ctx;

        // ── Embodiment (VirtualSelf, BodySchema, Controller) ──
        if (ctx.Config.EnableEmbodiment)
            await InitializeEmbodimentCoreAsync(ctx);
        else
            ctx.Output.RecordInit("Embodiment", false, "disabled");

        // ── Presence Detection ──
        await InitializePresenceDetectorCoreAsync(ctx);

        // ── Perception Tools + Vision ──
        InitializePerceptionAndVisionCore(ctx);

        // ── Interactive Avatar ──
        await InitializeAvatarCoreAsync(ctx);

        MarkInitialized();
    }

    private async Task InitializeEmbodimentCoreAsync(SubsystemInitContext ctx)
    {
        try
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Dim("  [>] Initializing Embodied Interaction..."));

            VirtualSelf = new VirtualSelf(ctx.Config.Persona);

            BodySchema = BodySchema.CreateMultimodal()
                .WithCapability(Capability.Hearing)
                .WithCapability(Capability.Speaking)
                .WithCapability(Capability.Seeing)
                .WithCapability(Capability.Reasoning)
                .WithLimitation(new Limitation(
                    LimitationType.ActionRestricted,
                    "Limited physical actuation - can look around via PTZ cameras but cannot manipulate objects",
                    0.5));

            // Register Tapo cameras from static configuration
            if (ctx.StaticConfiguration != null)
            {
                var tapoDeviceSection = ctx.StaticConfiguration.GetSection("Tapo:Devices");
                var tapoDeviceConfigs = tapoDeviceSection.GetChildren().ToList();
                if (tapoDeviceConfigs.Count > 0)
                {
                    foreach (var deviceSection in tapoDeviceConfigs)
                    {
                        var ip = deviceSection["ip_addr"] ?? "unknown";
                        var name = deviceSection["name"] ?? ip;
                        BodySchema = BodySchema.WithSensor(
                            SensorDescriptor.Visual($"tapo-cam-{ip}", $"Tapo Camera ({name})"));
                        BodySchema = BodySchema.WithActuator(
                            new ActuatorDescriptor(
                                $"tapo-ptz-{ip}", ActuatorModality.Motor,
                                $"PTZ Motor ({name}) - pan left/right, tilt up/down", true,
                                (IReadOnlySet<Capability>)new HashSet<Capability>()));
                    }
                    AnsiConsole.MarkupLine(OuroborosTheme.Dim($"    Tapo cameras: {tapoDeviceConfigs.Count} registered (RTSP + PTZ)"));
                }
            }

            Controller = new EmbodimentController(VirtualSelf, BodySchema);

            // Wire perception to personality (if available)
            if (ctx.Memory.PersonalityEngine != null)
            {
                VirtualSelf.Perceptions.Subscribe(perception =>
                {
                    System.Diagnostics.Debug.WriteLine($"[Embodiment] Perception: {perception.Modality} id={perception.Id}");
                });
            }

            // Fused perceptions → global workspace
            if (ctx.Autonomy.GlobalWorkspace != null)
            {
                VirtualSelf.FusedPerceptions.Subscribe(fused =>
                {
                    System.Diagnostics.Debug.WriteLine($"[Embodiment] Fused perception: confidence={fused.Confidence:F2}, modalities={fused.DominantModality}");
                });
            }

            if (ctx.Config.Debug)
            {
                Controller.Perceptions.Subscribe(perception =>
                {
                    AnsiConsole.MarkupLine(OuroborosTheme.Dim($"  [Perception] {Markup.Escape(perception.Modality.ToString())}: source={Markup.Escape(perception.Source ?? "?")}"));
                });
            }

            var startResult = await Controller.StartAsync();
            startResult.Match(
                _ => ctx.Output.RecordInit("Embodiment", true, $"VirtualSelf '{ctx.Config.Persona}' ({BodySchema.Capabilities.Count} capabilities)"),
                error => AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  ⚠ Embodiment start warning: {Markup.Escape(error.ToString())}")));

            AnsiConsole.MarkupLine(OuroborosTheme.Dim($"    Capabilities: {Markup.Escape(string.Join(", ", BodySchema.Capabilities))}"));
            AnsiConsole.MarkupLine(OuroborosTheme.Dim($"    Sensors: {BodySchema.Sensors.Count} | Actuators: {BodySchema.Actuators.Count}"));
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  ⚠ Embodiment initialization failed: {Markup.Escape(ex.Message)}"));
            if (ctx.Config.Debug)
            {
                AnsiConsole.MarkupLine($"    [red]→ {Markup.Escape(ex.GetType().Name)}: {Markup.Escape(ex.Message)}[/]");
            }
        }
    }

    private async Task InitializePresenceDetectorCoreAsync(SubsystemInitContext ctx)
    {
        try
        {
            var config = new PresenceConfig
            {
                CheckIntervalSeconds = 5,
                PresenceThreshold = 0.5,
                UseWifi = true,
                UseCamera = false,
                UseInputActivity = true,
                InputIdleThresholdSeconds = 180,
            };

            PresenceDetector = new PresenceDetector(config);

            // Event wiring for presence/absence → done by agent mediator
            // (HandlePresenceDetectedAsync, etc.)

            PresenceDetector.OnStateChanged += (oldState, newState) =>
            {
                System.Diagnostics.Debug.WriteLine($"[Presence] State changed: {oldState} \u2192 {newState}");
            };

            PresenceDetector.Start();
            UserWasPresent = true;

            SkillCliSteps.SharedPresenceDetector = PresenceDetector;

            ctx.Output.RecordInit("Presence Detection", true, $"WiFi + Input (interval={config.CheckIntervalSeconds}s)");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  ⚠ Presence Detection: {Markup.Escape(ex.Message)}"));
        }
    }

    private void InitializePerceptionAndVisionCore(SubsystemInitContext ctx)
    {
        try
        {
            // Register perception tools for proactive screen/camera monitoring
            var perceptionTools = PerceptionTools.CreateAllTools().ToList();
            foreach (var tool in perceptionTools)
                ctx.Tools.Tools = ctx.Tools.Tools.WithTool(tool);

            // Vision service for AI-powered visual understanding
            VisionService = new VisionService(new VisionConfig
            {
                OllamaEndpoint = ctx.Config.Endpoint,
                OllamaVisionModel = "qwen3-vl:235b-cloud",
            });
            PerceptionTools.VisionService = VisionService;

            ctx.Output.RecordInit("Perception", true,
                $"{perceptionTools.Count} perception tools + vision");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  ⚠ Perception/Vision: {Markup.Escape(ex.Message)}"));
        }
    }

    private async Task InitializeAvatarCoreAsync(SubsystemInitContext ctx)
    {
        if (!ctx.Config.Avatar) return;

        try
        {
            var (service, videoStream, liveVision) = await Avatar.AvatarIntegration.CreateAndStartWithVisionAsync(
                ctx.Config.Persona,
                ctx.Config.AvatarPort,
                ollamaEndpoint: ctx.Config.Endpoint,
                visionModelName: "qwen3-vl:235b-cloud",
                visionModel: ctx.Models.VisionModel,
                virtualSelf: VirtualSelf,
                ct: CancellationToken.None);

            AvatarService = service;
            AvatarVideoStream = videoStream;
            LiveVisionStream = liveVision;

            ctx.Output.RecordInit("Avatar", true, $"port {ctx.Config.AvatarPort} + video stream + live vision");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  ⚠ Avatar: {Markup.Escape(ex.Message)}"));
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Stop presence detector
        if (PresenceDetector != null)
        {
            await PresenceDetector.StopAsync();
            PresenceDetector.Dispose();
        }

        // Stop embodiment controller
        if (Controller != null)
        {
            await Controller.StopAsync();
            Controller.Dispose();
        }

        // Dispose virtual self
        VirtualSelf?.Dispose();

        // Dispose live vision stream
        if (LiveVisionStream != null)
            await LiveVisionStream.DisposeAsync();

        // Dispose avatar video stream
        if (AvatarVideoStream != null)
            await AvatarVideoStream.DisposeAsync();

        // Dispose avatar
        if (AvatarService != null)
            await AvatarService.DisposeAsync();

        IsInitialized = false;
    }
}
