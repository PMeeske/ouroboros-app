// <copyright file="ImmersiveMode.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.CLI.Commands;

using System.Text;
using System.Text.RegularExpressions;
using LangChain.DocumentLoaders;
using LangChain.Providers.Ollama;
using Ouroboros.Abstractions.Agent;  // For interfaces: ISkillRegistry, IMemoryStore, ISafetyGuard, IUncertaintyRouter
using Ouroboros.Agent;
using Ouroboros.Agent.MetaAI;  // For concrete implementations and other types
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Domain;
using Ouroboros.Network;
using Ouroboros.Options;
using Ouroboros.Providers;
using Ouroboros.Providers.SpeechToText;
using Ouroboros.Providers.TextToSpeech;
using Ouroboros.Speech;
using Ouroboros.Application;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Personality.Consciousness;
using Ouroboros.Application.Services;
using Ouroboros.Application.Tools;
using Ouroboros.Core.DistinctionLearning;
using Ouroboros.Domain.DistinctionLearning;
using Ouroboros.Tools.MeTTa;
using static Ouroboros.Application.Tools.AutonomousTools;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Abstractions;
using Ouroboros.Abstractions.Monads;
using Ouroboros.Core.Configuration;
using Qdrant.Client;
using Spectre.Console;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

/// <summary>
/// Unified immersive AI persona experience combining:
/// - Consciousness and self-awareness simulation
/// - Skills management and execution
/// - Dynamic tool creation and intelligent learning
/// - Pipeline DSL execution
/// - Voice interaction with TTS/STT
/// - Persistent identity and memory
/// </summary>
public sealed partial class ImmersiveMode
{
    // ── Agent subsystem references ─────────────────────────────────────────────
    private Subsystems.IModelSubsystem? _modelsSub;
    private Subsystems.IToolSubsystem? _toolsSub;
    private Subsystems.IMemorySubsystem? _memorySub;
    private Subsystems.IAutonomySubsystem? _autonomySub;

    private ImmersivePersona? _configuredPersona;
    private Application.Avatar.InteractiveAvatarService? _configuredAvatarService;

    /// <summary>Last detected language culture (BCP-47), updated per user turn.</summary>
    private string _lastDetectedCulture = "en-US";

    /// <summary>The BCP-47 culture last detected from user input.</summary>
    public string? LastDetectedCulture => _lastDetectedCulture == "en-US" ? null : _lastDetectedCulture;

    /// <summary>
    /// Set to true while Iaret is speaking (TTS playback active).
    /// RoomMode checks this to suppress utterances that are Iaret's own voice
    /// picked up by the room microphone (acoustic echo prevention).
    /// </summary>
    public volatile bool IsSpeaking;

    /// <summary>
    /// True when running inside OuroborosAgent with pre-wired subsystems;
    /// false when running standalone (direct CLI invocation).
    /// </summary>
    private bool HasSubsystems => _modelsSub != null;

    // DI service provider for resolving cross-cutting services (Qdrant, etc.)
    private readonly IServiceProvider? _serviceProvider;

    // Skill registry for this session
    private ISkillRegistry? _skillRegistry;
    private DynamicToolFactory? _dynamicToolFactory;
    private IntelligentToolLearner? _toolLearner;
    private InterconnectedLearner? _interconnectedLearner;
    private QdrantSelfIndexer? _selfIndexer;
    private PersistentConversationMemory? _conversationMemory;
    private PersistentNetworkStateProjector? _networkStateProjector;
    private AutonomousMind? _autonomousMind;
    private SelfPersistence? _selfPersistence;
    private ToolRegistry _dynamicTools = new();
    private StringBuilder _currentInputBuffer = new();
    private readonly object _inputLock = new();
    private string _currentPromptPrefix = "  You: ";
    private IReadOnlyDictionary<string, PipelineTokenInfo>? _allTokens;
    private CliPipelineState? _pipelineState;
    private string? _lastPipelineContext; // Track recent pipeline interactions
    private (string Topic, string Description)? _pendingToolRequest; // Track pending tool creation context

    // Distinction learning
    private IDistinctionLearner? _distinctionLearner;
    private ConsciousnessDream? _dream;
    private DistinctionState _currentDistinctionState = DistinctionState.Initial();

    // Multi-model orchestration and divide-and-conquer
    private OrchestratedChatModel? _orchestratedModel;
    private DivideAndConquerOrchestrator? _divideAndConquer;
    private IChatCompletionModel? _baseModel;

    // Avatar + persona event wiring (owned by ImmersiveSubsystem)
    private Subsystems.ImmersiveSubsystem? _immersive;

    // Ethics + CognitivePhysics + Phi — integrated into every response turn
    private Ouroboros.Core.Ethics.IEthicsFramework? _immersiveEthics;
    private Ouroboros.Core.CognitivePhysics.CognitivePhysicsEngine? _immersiveCogPhysics;
    private Ouroboros.Core.CognitivePhysics.CognitiveState _immersiveCogState
        = Ouroboros.Core.CognitivePhysics.CognitiveState.Create("general");
    private Ouroboros.Providers.IITPhiCalculator _immersivePhiCalc = new();
    private string _immersiveLastTopic = "general";
    private Ouroboros.Pipeline.Memory.IEpisodicMemoryEngine? _episodicMemory;
    private readonly Ouroboros.Pipeline.Metacognition.MetacognitiveReasoner _metacognition = new();
    private Ouroboros.Agent.NeuralSymbolic.INeuralSymbolicBridge? _neuralSymbolicBridge;
    private Ouroboros.Core.Reasoning.ICausalReasoningEngine _causalReasoning = new Ouroboros.Core.Reasoning.CausalReasoningEngine();
    private Ouroboros.Agent.MetaAI.ICuriosityEngine? _curiosityEngine;
    private int _immersiveResponseCount;
    private Ouroboros.CLI.Sovereignty.PersonaSovereigntyGate? _sovereigntyGate;

    // ── Constructors ────────────────────────────────────────────────────────

    /// <summary>
    /// Static entry point for the 'immersive' CLI command.
    /// Creates a standalone ImmersiveMode instance and runs it.
    /// </summary>
    public static async Task RunImmersiveAsync(
        Ouroboros.Options.ImmersiveCommandVoiceOptions opts,
        CancellationToken ct = default)
    {
        var mode = new ImmersiveMode();
        await mode.RunAsync(opts, ct);
    }

    /// <summary>
    /// Creates a standalone ImmersiveMode session (no pre-wired subsystems).
    /// </summary>
    public ImmersiveMode() { }

    /// <summary>
    /// Creates an ImmersiveMode session wired to OuroborosAgent's subsystems.
    /// </summary>
    public ImmersiveMode(
        Subsystems.IModelSubsystem models,
        Subsystems.IToolSubsystem tools,
        Subsystems.IMemorySubsystem memory,
        Subsystems.IAutonomySubsystem autonomy,
        ImmersivePersona? persona = null,
        Application.Avatar.InteractiveAvatarService? avatarService = null,
        IServiceProvider? serviceProvider = null)
    {
        _modelsSub = models;
        _toolsSub = tools;
        _memorySub = memory;
        _autonomySub = autonomy;
        _configuredPersona = persona;
        _configuredAvatarService = avatarService;
        _serviceProvider = serviceProvider;

        // Wire shared instances from subsystems
        _orchestratedModel = models.OrchestratedModel;
        _divideAndConquer = models.DivideAndConquer;
        _baseModel = models.ChatModel;
        _skillRegistry = memory.Skills;
        _dynamicToolFactory = tools.ToolFactory;
        _toolLearner = tools.ToolLearner;
        _selfIndexer = autonomy.SelfIndexer;
        _autonomousMind = autonomy.AutonomousMind;
        if (tools.Tools.Count > 0) _dynamicTools = tools.Tools;
    }

    // ── Room mode hooks ─────────────────────────────────────────────────────

    /// <summary>
    /// Displays a room interjection from Iaret in the foreground chat pane.
    /// Subscribed to <see cref="Services.RoomPresence.RoomIntentBus.OnIaretInterjected"/>.
    /// </summary>
    public void ShowRoomInterjection(string personaName, string speech)
    {
        AnsiConsole.MarkupLine($"\n  [darkgreen][[room]] {Markup.Escape(personaName)}: {Markup.Escape(speech)}[/]");
    }

    /// <summary>
    /// Displays when someone in the room addresses Iaret directly by name.
    /// Subscribed to <see cref="Services.RoomPresence.RoomIntentBus.OnUserAddressedIaret"/>.
    /// </summary>
    public void ShowRoomAddress(string speaker, string utterance)
    {
        AnsiConsole.MarkupLine($"\n  [darkcyan][[room\u2192Iaret]] {Markup.Escape(speaker)}: {Markup.Escape(utterance)}[/]");
    }
}
