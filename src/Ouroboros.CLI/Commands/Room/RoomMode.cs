// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Commands;

using Ouroboros.Application.Configuration;
using Ouroboros.Application.Personality;
using Ouroboros.CLI.Commands.Options;
using Ouroboros.CLI.Services.RoomPresence;
using Ouroboros.CLI.Subsystems;
using Ouroboros.Core.CognitivePhysics;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Parsing;

/// <summary>
/// Iaret as ambient AI room presence.
///
/// Continuously listens to the room microphone, identifies speakers by
/// communication style (persisted to Qdrant via PersonalityEngine), and
/// proactively interjects using a five-stage pipeline:
///
///   Ethics gate  -> CognitivePhysics shift cost  -> Phi (IIT)  -> LLM decision  -> TTS + console
///
/// When <c>--proactive</c> is enabled (default), Iaret also speaks during
/// prolonged silence, reacts to camera-based presence, and responds to gestures.
///
/// Launch with: <c>ouroboros room</c> or <c>ouroboros room --quiet</c>
/// </summary>
public sealed partial class RoomMode
{
    // Interjection rate limiting + CogPhysics state (reset on each RunAsync call)
    private DateTime _lastInterjection = DateTime.MinValue;
    private readonly Queue<DateTime> _recentInterjections = new();
    private CognitiveState _roomCogState = CognitiveState.Create("general");
    private string _roomLastTopic = "general";
    private Ouroboros.Pipeline.Memory.IEpisodicMemoryEngine? _roomEpisodic;
    private readonly Ouroboros.Pipeline.Metacognition.MetacognitiveReasoner _roomMetacognition = new();
    private Ouroboros.Agent.NeuralSymbolic.INeuralSymbolicBridge? _roomNeuralSymbolic;
    private Ouroboros.Core.Reasoning.ICausalReasoningEngine _roomCausalReasoning = new Ouroboros.Core.Reasoning.CausalReasoningEngine();
    private Ouroboros.Agent.MetaAI.ICuriosityEngine? _roomCuriosity;
    private Ouroboros.CLI.Sovereignty.PersonaSovereigntyGate? _roomSovereigntyGate;

    // ── Agent subsystem references ────────────────────────────────────────────
    private readonly IModelSubsystem?    _agentModels;
    private readonly IMemorySubsystem?   _agentMemory;
    private readonly IAutonomySubsystem? _agentAutonomy;
    private readonly IServiceProvider?   _serviceProvider;

    // ── ImmersiveMode reference for IsSpeaking check ──────────────────────────
    private readonly ImmersiveMode? _immersiveMode;

    // ── Voice signature service (speaker biometric identification) ─────────────
    private readonly VoiceSignatureService _voiceSignatures = new();

    // ── Proactive silence tracking ────────────────────────────────────────────
    private DateTime _lastUtteranceTime = DateTime.UtcNow;
    private DateTime _lastProactiveSpeech = DateTime.MinValue;

    // ── Presence detection ────────────────────────────────────────────────────
    private readonly PresenceDetector? _presenceDetector;

    /// <summary>
    /// Creates a RoomMode instance wired to the agent's subsystems.
    /// </summary>
    public RoomMode(
        ImmersiveMode?      immersiveMode    = null,
        IModelSubsystem?    agentModels      = null,
        IMemorySubsystem?   agentMemory      = null,
        IAutonomySubsystem? agentAutonomy    = null,
        IServiceProvider?   serviceProvider  = null,
        PresenceDetector?   presenceDetector = null)
    {
        _immersiveMode    = immersiveMode;
        _agentModels      = agentModels;
        _agentMemory      = agentMemory;
        _agentAutonomy    = agentAutonomy;
        _serviceProvider  = serviceProvider;
        _presenceDetector = presenceDetector;
    }

    /// <summary>
    /// Entry point wired by Program.cs. Parses the System.CommandLine result
    /// and starts the room presence loop.
    /// </summary>
    public Task RunAsync(ParseResult parseResult, RoomCommandOptions opts, CancellationToken ct)
    {
        var personaName  = parseResult.GetValue(opts.PersonaOption) ?? "Iaret";
        var model        = parseResult.GetValue(opts.ModelOption) ?? "deepseek-v3.1:671b-cloud";
        var endpoint     = parseResult.GetValue(opts.EndpointOption) ?? DefaultEndpoints.Ollama;
        var embedModel   = parseResult.GetValue(opts.EmbedModelOption) ?? "nomic-embed-text";
        var qdrant       = parseResult.GetValue(opts.QdrantEndpointOption) ?? DefaultEndpoints.QdrantGrpc;
        var speechKey    = parseResult.GetValue(opts.AzureSpeechKeyOption)
                          ?? Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
        var speechRegion = parseResult.GetValue(opts.AzureSpeechRegionOption) ?? "eastus";
        var ttsVoice     = parseResult.GetValue(opts.TtsVoiceOption) ?? "en-US-JennyMultilingualNeural";
        var localTts     = parseResult.GetValue(opts.LocalTtsOption);
        var avatarOn     = parseResult.GetValue(opts.AvatarOption);
        var avatarPort   = parseResult.GetValue(opts.AvatarPortOption);
        var quiet        = parseResult.GetValue(opts.QuietOption);
        var cooldown     = TimeSpan.FromSeconds(parseResult.GetValue(opts.CooldownOption));
        var maxPer10     = parseResult.GetValue(opts.MaxInterjectionsOption);
        var phiThreshold = parseResult.GetValue(opts.PhiThresholdOption);
        var proactive    = parseResult.GetValue(opts.ProactiveOption);
        var idleDelay    = TimeSpan.FromSeconds(parseResult.GetValue(opts.IdleDelayOption));
        var enableCamera = parseResult.GetValue(opts.CameraOption);

        return RunAsync(
            personaName, model, endpoint, embedModel, qdrant,
            speechKey, speechRegion, ttsVoice, localTts,
            avatarOn, avatarPort,
            quiet, cooldown, maxPer10, phiThreshold,
            proactive, idleDelay, enableCamera, ct);
    }

    /// <summary>
    /// Convenience entry point accepting a <see cref="RoomConfig"/> record.
    /// Parallels <see cref="ImmersiveMode.RunImmersiveAsync"/> for consistent service invocation.
    /// </summary>
    public Task RunAsync(RoomConfig config, CancellationToken ct = default)
        => RunAsync(
            personaName:      config.Persona,
            model:            config.Model,
            endpoint:         config.Endpoint,
            embedModel:       config.EmbedModel,
            qdrant:           config.QdrantEndpoint,
            azureSpeechKey:   config.AzureSpeechKey,
            azureSpeechRegion: config.AzureSpeechRegion,
            ttsVoice:         config.TtsVoice,
            localTts:         config.LocalTts,
            avatarOn:         config.Avatar,
            avatarPort:       config.AvatarPort,
            quiet:            config.Quiet,
            cooldown:         TimeSpan.FromSeconds(config.CooldownSeconds),
            maxPerWindow:     config.MaxInterjections,
            phiThreshold:     config.PhiThreshold,
            proactiveMode:    config.Proactive,
            idleSpeechDelay:  TimeSpan.FromSeconds(config.IdleDelaySeconds),
            enableCamera:     config.EnableCamera,
            ct:               ct);
}
