// <copyright file="OuroborosAgent.Init.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Spectre.Console;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Agent initialization orchestration and cross-subsystem dependency wiring.
/// Autonomy wiring is in <see cref="OuroborosAgent"/> (OuroborosAgent.AutoWiring.cs).
/// Event wiring is in <see cref="OuroborosAgent"/> (OuroborosAgent.EventWiring.cs).
/// </summary>
public sealed partial class OuroborosAgent
{
    /// <summary>
    /// Initializes all agent subsystems via mediator delegation.
    /// Each subsystem self-initializes; agent wires cross-subsystem dependencies.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        // Set static culture for TTS in static methods
        SetStaticCulture(_config.Culture);

        _output.WriteWelcome(_config.Persona, _config.Model,
            _voice.ActivePersona?.Moods?.FirstOrDefault());

        // Print feature configuration (verbose only)
        if (_config.Verbosity == OutputVerbosity.Verbose)
            PrintFeatureStatus();

        // Crush-inspired: shared permission broker and agent event bus.
        // SkipAll mirrors the existing YoloMode flag (--yolo = bypass all approvals).
        var permissionBroker = new CLI.Infrastructure.ToolPermissionBroker
        {
            SkipAll = _config.YoloMode,
        };
        _permissionBroker = permissionBroker;
        _ = _permissionBroker; // S4487: retained for broker lifetime
        var agentEventBus = new CLI.Infrastructure.EventBroker<CLI.Infrastructure.AgentEvent>();

        // Create shared initialization context (mediator pattern)
        var ctx = new Subsystems.SubsystemInitContext
        {
            Config = _config,
            Output = _output,
            VoiceService = _voice,
            StaticConfiguration = _staticConfiguration,
            Services = _serviceProvider,
            Voice = _voiceSub,
            Models = _modelsSub,
            Tools = _toolsSub,
            Memory = _memorySub,
            Cognitive = _cognitiveSub,
            Autonomy = _autonomySub,
            Embodiment = _embodimentSub,
            RegisterCameraCaptureAction = () => RegisterCameraCaptureTool(),
            PermissionBroker = permissionBroker,
            AgentEventBus    = agentEventBus,
        };

        // -- Phase 1: Infrastructure (standalone) --
        if (_config.Voice)
            await _voice.InitializeAsync();

        _voiceSub.SpeakWithSapiFunc = SpeakWithSapiAsync;
        await _voiceSub.InitializeAsync(ctx);

        // -- Phase 2: Models (standalone) --
        await _modelsSub.InitializeAsync(ctx);

        // -- Phase 2.5: Localization + Language detection (need Config; Language Llm is remote) --
        await _localizationSub.InitializeAsync(ctx);
        await _languageSub.InitializeAsync(ctx);

        // Wire LLM sanitizer on voice side channel
        if (_config.VoiceChannel && _voiceSideChannel != null && _chatModel != null)
        {
            _voiceSideChannel.SetLlmSanitizer(async (prompt, ct) =>
                await _chatModel.GenerateTextAsync(prompt, ct));
            _output.RecordInit("Voice LLM Sanitizer", true, "natural speech condensation");
        }

        // -- Phase 3: Tools (needs Models) --
        await _toolsSub.InitializeAsync(ctx);

        // -- Phase 3.1: Claude-style meta-tools (plan / ask / bypass) --
        RegisterClaudeStyleTools();

        // -- Phase 4: Memory (needs Models, uses MeTTa from Tools) --
        await _memorySub.InitializeAsync(ctx);

        // -- Phase 5: Cognitive (needs Models + Memory + Tools) --
        await _cognitiveSub.InitializeAsync(ctx);

        // -- Phase 6: Autonomy (needs all above) --
        await _autonomySub.InitializeAsync(ctx);

        // -- Phase 7: Embodiment (needs Memory + Autonomy) --
        await _embodimentSub.InitializeAsync(ctx);

        // -- Phase 7.1: SelfAssembly (needs Autonomy; Llm + History wired after Phase 8) --
        await _selfAssemblySub.InitializeAsync(ctx);

        // -- Phase 7.2: PipeProcessing (needs Config; ProcessInputFunc wired after Phase 8) --
        await _pipeSub.InitializeAsync(ctx);

        // -- Phase 7.3: Chat (needs all original subsystems) --
        await _chatSub.InitializeAsync(ctx);

        // -- Phase 7.4: CommandRouting (needs Tools, Memory, Autonomy) --
        await _commandRoutingSub.InitializeAsync(ctx);

        // -- Phase 8: Cross-subsystem wiring (mediator orchestration) --
        await WireCrossSubsystemDependenciesAsync();

        // -- Phase 8.5: Agent event bridge (MediatR notification pipeline) --
        WireAgentEventBridge(agentEventBus);

        // -- Phase 9: Post-init actions --
        _isInitialized = true;
        _output.FlushInitSummary();
        if (_config.Verbosity != OutputVerbosity.Quiet)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Ok("\n  ✓ Ouroboros fully initialized\n"));
            PrintQuickHelp();
        }

        // AGI warmup - prime the model with examples for autonomous operation
        await PerformAgiWarmupAsync();

        // Enforce policies if self-modification is enabled
        if (_config.EnableSelfModification)
            await EnforceGovernancePoliciesAsync();

        // Start listening for voice input if enabled via CLI
        if (_config.Listen)
        {
            _output.WriteSystem("🎤 Voice listening enabled via --listen flag");
            await StartListeningAsync();
        }
    }

    /// <summary>
    /// Wires cross-subsystem dependencies that require mediator orchestration.
    /// This is the core of the mediator pattern -- connecting subsystem components.
    /// </summary>
    private async Task WireCrossSubsystemDependenciesAsync()
    {
        // Autonomy subsystem cross-references
        WireAutonomyCallbacks();

        // Autonomous Mind delegates
        await WireAutonomousMindDelegatesAsync();

        // Autonomous Action Engine (on by default, 3-min interval)
        WireAutonomousActionEngine();

        // Autonomous Coordinator
        await WireAutonomousCoordinatorAsync();

        // Self-Execution
        if (_config.EnableMind)
            WireSelfExecution();

        // Push Mode
        if (_config.EnablePush)
            WirePushMode();

        // Presence Detection events
        WirePresenceDetection();

        // Persona events
        WirePersonaEvents();

        // SystemAccessTools shared state
        WireSystemAccessTools();

        // Network state persistence delegates
        WireNetworkPersistence();

        // Avatar mood transitions
        WireAvatarMoodTransitions();

        // Pipeline think delegate
        WirePipelineThinkDelegate();

        // Memory subsystem thought persistence
        _memorySub.PersistThoughtFunc = PersistThoughtAsync;

        // Localization: wire LLM for thought translation
        _localizationSub.Llm = _llm;

        // SelfAssembly: wire LLM, conversation history, and event callbacks
        _selfAssemblySub.Llm = _llm;
        _selfAssemblySub.ConversationHistory = _memorySub.ConversationHistory;
        _selfAssemblySub.WireCallbacks();

        // PipeProcessing: wire the central input dispatch function
        _pipeSub.ProcessInputFunc = ProcessInputAsync;

        // Chat: wire persistence delegates and language lookup
        _chatSub.PersistThoughtFunc = PersistThoughtAsync;
        _chatSub.PersistThoughtResultFunc = (id, type, content, success, confidence) =>
            PersistThoughtResultAsync(id, type, content, success, confidence);
        _chatSub.GetLanguageNameFunc = _localizationSub.GetLanguageName;

        // MeTTa: shared orchestrator -- persists atom state across tool calls
        _mettaOrchestrator = new Ouroboros.Application.Services.ParallelMeTTaThoughtStreams(maxParallelism: 5);
        if (_chatModel != null)
            _mettaOrchestrator.ConnectOllama(async (p, ct) => await _chatModel.GenerateTextAsync(p, ct));
        Ouroboros.Application.Tools.AutonomousTools.DefaultContext.MeTTaOrchestrator = _mettaOrchestrator;

        // verify_claim: always-on search wiring (fallback when mind is disabled)
        var toolCtx = Ouroboros.Application.Tools.AutonomousTools.DefaultContext;
        toolCtx.SearchFunction ??= async (query, ct) =>
        {
            var t = _toolFactory?.CreateWebSearchTool("duckduckgo");
            if (t == null) return string.Empty;
            var r = await t.InvokeAsync(query, ct).ConfigureAwait(false);
            return r.Match(s => s, _ => string.Empty);
        };
        toolCtx.EvaluateFunction ??= async (p, ct) =>
            _chatModel != null ? await _chatModel.GenerateTextAsync(p, ct) : string.Empty;

        // Cognitive Thought Streams (Rx -- bridges all event sources)
        WireCognitiveStream();
    }

    /// <summary>Gets the language name for a given culture code.</summary>
    private string GetLanguageName(string culture) => _localizationSub.GetLanguageName(culture);

    /// <summary>Gets the default Azure TTS voice name for a given culture code.</summary>
    private string GetDefaultVoiceForCulture(string? culture) =>
        _localizationSub.GetDefaultVoiceForCulture(culture);

    /// <summary>Gets the effective TTS voice, considering culture override.</summary>
    private string GetEffectiveVoice() => _localizationSub.GetEffectiveVoice();

    /// <summary>Translates a thought to the target language if culture is specified.</summary>
    private Task<string> TranslateThoughtIfNeededAsync(string thought) =>
        _localizationSub.TranslateThoughtIfNeededAsync(thought);
}
