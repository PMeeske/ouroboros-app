                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                // <copyright file="OuroborosAgent.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using IEmbeddingModel = Ouroboros.Domain.IEmbeddingModel;
using PipelineReasoningStep = Ouroboros.Domain.Events.ReasoningStep;
// Type aliases to resolve ambiguities between Agent.MetaAI and Pipeline namespaces
using PipelineGoal = Ouroboros.Pipeline.Planning.Goal;
// Keep MetaAI types accessible with explicit names for existing code
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;
using LangChain.DocumentLoaders;
using Ouroboros.Abstractions.Monads;

namespace Ouroboros.CLI.Commands;

#region Runtime Prompt Optimization System

#endregion

/// <summary>
/// Unified Ouroboros agent that integrates all capabilities:
/// - Voice interaction (TTS/STT)
/// - Skill-based learning
/// - MeTTa symbolic reasoning
/// - Dynamic tool creation
/// - Personality engine with affective states
/// - Self-improvement and curiosity
/// - Persistent thought memory across sessions
/// </summary>
public sealed partial class OuroborosAgent : IAsyncDisposable, IAgentFacade
{
    private readonly OuroborosConfig _config;
    private readonly IConsoleOutput _output;
    private readonly VoiceModeService _voice;

    // Static configuration for Azure credentials (set from OuroborosCommands)
    private static Microsoft.Extensions.Configuration.IConfiguration? _staticConfiguration;

    // Static culture for TTS voice selection in static methods
    private static string? _staticCulture;

    /// <summary>
    /// Sets the configuration for Azure Speech and other services.
    /// </summary>
    public static void SetConfiguration(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _staticConfiguration = configuration;
    }

    /// <summary>
    /// Sets the culture for voice synthesis in static methods.
    /// </summary>
    public static void SetStaticCulture(string? culture)
    {
        _staticCulture = culture;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SUBSYSTEM-BACKED PROPERTY PROXIES
    // Each property delegates to the owning subsystem — no SyncSubsystems needed.
    // ═══════════════════════════════════════════════════════════════════════════

    // ── Models ──
    private IChatCompletionModel? _chatModel { get => _modelsSub.ChatModel; set => _modelsSub.ChatModel = value; }
    private ToolAwareChatModel? _llm { get => _modelsSub.Llm; set => _modelsSub.Llm = value; }
    private IEmbeddingModel? _embedding { get => _modelsSub.Embedding; set => _modelsSub.Embedding = value; }
    private OrchestratedChatModel? _orchestratedModel { get => _modelsSub.OrchestratedModel; set => _modelsSub.OrchestratedModel = value; }
    private DivideAndConquerOrchestrator? _divideAndConquer { get => _modelsSub.DivideAndConquer; set => _modelsSub.DivideAndConquer = value; }
    private IChatCompletionModel? _coderModel { get => _modelsSub.CoderModel; set => _modelsSub.CoderModel = value; }
    private IChatCompletionModel? _reasonModel { get => _modelsSub.ReasonModel; set => _modelsSub.ReasonModel = value; }
    private IChatCompletionModel? _summarizeModel { get => _modelsSub.SummarizeModel; set => _modelsSub.SummarizeModel = value; }
    private IChatCompletionModel? _visionChatModel { get => _modelsSub.VisionChatModel; set => _modelsSub.VisionChatModel = value; }
    private IVisionModel? _visionModel { get => _modelsSub.VisionModel; set => _modelsSub.VisionModel = value; }
    private LlmCostTracker? _costTracker { get => _modelsSub.CostTracker; set => _modelsSub.CostTracker = value; }

    // ── Tools ──
    private ToolRegistry _tools { get => _toolsSub.Tools; set => _toolsSub.Tools = value; }
    private PromptOptimizer _promptOptimizer => _toolsSub.PromptOptimizer;
    private DynamicToolFactory? _toolFactory { get => _toolsSub.ToolFactory; set => _toolsSub.ToolFactory = value; }
    private IntelligentToolLearner? _toolLearner { get => _toolsSub.ToolLearner; set => _toolsSub.ToolLearner = value; }
    private SmartToolSelector? _smartToolSelector { get => _toolsSub.SmartToolSelector; set => _toolsSub.SmartToolSelector = value; }
    private ToolCapabilityMatcher? _toolCapabilityMatcher { get => _toolsSub.ToolCapabilityMatcher; set => _toolsSub.ToolCapabilityMatcher = value; }
    private PlaywrightMcpTool? _playwrightTool { get => _toolsSub.PlaywrightTool; set => _toolsSub.PlaywrightTool = value; }

    // ── Memory ──
    private ISkillRegistry? _skills { get => _memorySub.Skills; set => _memorySub.Skills = value; }
    private IMeTTaEngine? _mettaEngine { get => _memorySub.MeTTaEngine; set => _memorySub.MeTTaEngine = value; }
    private PersonalityEngine? _personalityEngine { get => _memorySub.PersonalityEngine; set => _memorySub.PersonalityEngine = value; }
    private PersonalityProfile? _personality { get => _memorySub.Personality; set => _memorySub.Personality = value; }
    private IValenceMonitor? _valenceMonitor { get => _memorySub.ValenceMonitor; set => _memorySub.ValenceMonitor = value; }
    private ThoughtPersistenceService? _thoughtPersistence { get => _memorySub.ThoughtPersistence; set => _memorySub.ThoughtPersistence = value; }
    private List<InnerThought> _persistentThoughts { get => _memorySub.PersistentThoughts; set => _memorySub.PersistentThoughts = value; }
    private string? _lastThoughtContent { get => _memorySub.LastThoughtContent; set => _memorySub.LastThoughtContent = value; }
    private QdrantNeuralMemory? _neuralMemory { get => _memorySub.NeuralMemory; set => _memorySub.NeuralMemory = value; }
    private List<string> _conversationHistory => _memorySub.ConversationHistory;

    // ── Cognitive ──
    private ImmersivePersona? _immersivePersona { get => _cognitiveSub.ImmersivePersona; set => _cognitiveSub.ImmersivePersona = value; }
    private ContinuouslyLearningAgent? _learningAgent { get => _cognitiveSub.LearningAgent; set => _cognitiveSub.LearningAgent = value; }
    private AdaptiveMetaLearner? _metaLearner { get => _cognitiveSub.MetaLearner; set => _cognitiveSub.MetaLearner = value; }
    private ExperienceBuffer? _experienceBuffer { get => _cognitiveSub.ExperienceBuffer; set => _cognitiveSub.ExperienceBuffer = value; }
    private RealtimeCognitiveMonitor? _cognitiveMonitor { get => _cognitiveSub.CognitiveMonitor; set => _cognitiveSub.CognitiveMonitor = value; }
    private BayesianSelfAssessor? _selfAssessor { get => _cognitiveSub.SelfAssessor; set => _cognitiveSub.SelfAssessor = value; }
    private CognitiveIntrospector? _introspector { get => _cognitiveSub.Introspector; set => _cognitiveSub.Introspector = value; }
    private CouncilOrchestrator? _councilOrchestrator { get => _cognitiveSub.CouncilOrchestrator; set => _cognitiveSub.CouncilOrchestrator = value; }
    private AgentCoordinator? _agentCoordinator { get => _cognitiveSub.AgentCoordinator; set => _cognitiveSub.AgentCoordinator = value; }
    private WorldState? _worldState { get => _cognitiveSub.WorldState; set => _cognitiveSub.WorldState = value; }

    // ── Autonomy ──
    private MetaAIPlannerOrchestrator? _orchestrator { get => _autonomySub.Orchestrator; set => _autonomySub.Orchestrator = value; }
    private AutonomousMind? _autonomousMind { get => _autonomySub.AutonomousMind; set => _autonomySub.AutonomousMind = value; }
    private AutonomousCoordinator? _autonomousCoordinator { get => _autonomySub.Coordinator; set => _autonomySub.Coordinator = value; }
    private ConcurrentQueue<AutonomousGoal> _goalQueue => _autonomySub.GoalQueue;
    private Task? _selfExecutionTask { get => _autonomySub.SelfExecutionTask; set => _autonomySub.SelfExecutionTask = value; }
    private CancellationTokenSource? _selfExecutionCts { get => _autonomySub.SelfExecutionCts; set => _autonomySub.SelfExecutionCts = value; }
    private bool _selfExecutionEnabled { get => _autonomySub.SelfExecutionEnabled; set => _autonomySub.SelfExecutionEnabled = value; }
    private ConcurrentDictionary<string, SubAgentInstance> _subAgents => _autonomySub.SubAgents;
    private IDistributedOrchestrator? _distributedOrchestrator { get => _autonomySub.DistributedOrchestrator; set => _autonomySub.DistributedOrchestrator = value; }
    private IEpicBranchOrchestrator? _epicOrchestrator { get => _autonomySub.EpicOrchestrator; set => _autonomySub.EpicOrchestrator = value; }
    private IIdentityGraph? _identityGraph { get => _autonomySub.IdentityGraph; set => _autonomySub.IdentityGraph = value; }
    private IGlobalWorkspace? _globalWorkspace { get => _autonomySub.GlobalWorkspace; set => _autonomySub.GlobalWorkspace = value; }
    private IPredictiveMonitor? _predictiveMonitor { get => _autonomySub.PredictiveMonitor; set => _autonomySub.PredictiveMonitor = value; }
    private ISelfEvaluator? _selfEvaluator { get => _autonomySub.SelfEvaluator; set => _autonomySub.SelfEvaluator = value; }
    private ICapabilityRegistry? _capabilityRegistry { get => _autonomySub.CapabilityRegistry; set => _autonomySub.CapabilityRegistry = value; }
    private SelfAssemblyEngine? _selfAssemblyEngine { get => _autonomySub.SelfAssemblyEngine; set => _autonomySub.SelfAssemblyEngine = value; }
    private BlueprintAnalyzer? _blueprintAnalyzer { get => _autonomySub.BlueprintAnalyzer; set => _autonomySub.BlueprintAnalyzer = value; }
    private MeTTaBlueprintValidator? _blueprintValidator { get => _autonomySub.BlueprintValidator; set => _autonomySub.BlueprintValidator = value; }
    private QdrantSelfIndexer? _selfIndexer { get => _autonomySub.SelfIndexer; set => _autonomySub.SelfIndexer = value; }
    private NetworkStateTracker? _networkTracker { get => _autonomySub.NetworkTracker; set => _autonomySub.NetworkTracker = value; }
    private Task? _pushModeTask { get => _autonomySub.PushModeTask; set => _autonomySub.PushModeTask = value; }
    private CancellationTokenSource? _pushModeCts { get => _autonomySub.PushModeCts; set => _autonomySub.PushModeCts = value; }
    private PersistentNetworkStateProjector? _networkProjector { get => _autonomySub.NetworkProjector; set => _autonomySub.NetworkProjector = value; }

    //  Memory (extended)
    private PersistentConversationMemory? _conversationMemory { get => _memorySub.ConversationMemory; set => _memorySub.ConversationMemory = value; }
    private SelfPersistence? _selfPersistence { get => _memorySub.SelfPersistence; set => _memorySub.SelfPersistence = value; }

    //  Cognitive (extended)
    private Ouroboros.Core.DistinctionLearning.IDistinctionLearner? _distinctionLearner { get => _cognitiveSub.DistinctionLearner; set => _cognitiveSub.DistinctionLearner = value; }
    private Ouroboros.Application.Personality.Consciousness.ConsciousnessDream? _dream { get => _cognitiveSub.Dream; set => _cognitiveSub.Dream = value; }
    private Ouroboros.Core.DistinctionLearning.DistinctionState _currentDistinctionState { get => _cognitiveSub.CurrentDistinctionState; set => _cognitiveSub.CurrentDistinctionState = value; }
    private InterconnectedLearner? _interconnectedLearner { get => _cognitiveSub.InterconnectedLearner; set => _cognitiveSub.InterconnectedLearner = value; }

    //  Embodiment (extended)
    private Application.Avatar.InteractiveAvatarService? _avatarService { get => _embodimentSub.AvatarService; set => _embodimentSub.AvatarService = value; }
    private VisionService? _visionService { get => _embodimentSub.VisionService; set => _embodimentSub.VisionService = value; }

    //  Tools (extended)
    private IReadOnlyDictionary<string, PipelineTokenInfo>? _allPipelineTokens { get => _toolsSub.AllPipelineTokens; set => _toolsSub.AllPipelineTokens = value; }
    private CliPipelineState? _pipelineState { get => _toolsSub.PipelineState; set => _toolsSub.PipelineState = value; }


    // ── Voice ──
    private VoiceModeServiceV2? _voiceV2 { get => _voiceSub.V2; set => _voiceSub.V2 = value; }
    private VoiceSideChannel? _voiceSideChannel { get => _voiceSub.SideChannel; set => _voiceSub.SideChannel = value; }
    private Ouroboros.CLI.Services.EnhancedListeningService? _enhancedListener { get => _voiceSub.Listener; set => _voiceSub.Listener = value; }
    private CancellationTokenSource? _listeningCts { get => _voiceSub.ListeningCts; set => _voiceSub.ListeningCts = value; }
    private Task? _listeningTask { get => _voiceSub.ListeningTask; set => _voiceSub.ListeningTask = value; }
    private bool _isListening { get => _voiceSub.IsListening; set => _voiceSub.IsListening = value; }

    // ── Embodiment ──
    private EmbodimentController? _embodimentController { get => _embodimentSub.Controller; set => _embodimentSub.Controller = value; }
    private VirtualSelf? _virtualSelf { get => _embodimentSub.VirtualSelf; set => _embodimentSub.VirtualSelf = value; }
    private BodySchema? _bodySchema { get => _embodimentSub.BodySchema; set => _embodimentSub.BodySchema = value; }
    private Ouroboros.Providers.Tapo.ITapoRtspClientFactory? _tapoRtspFactory { get => _embodimentSub.TapoRtspFactory; set => _embodimentSub.TapoRtspFactory = value; }
    private Ouroboros.Providers.Tapo.TapoRestClient? _tapoRestClient { get => _embodimentSub.TapoRestClient; set => _embodimentSub.TapoRestClient = value; }
    private PresenceDetector? _presenceDetector { get => _embodimentSub.PresenceDetector; set => _embodimentSub.PresenceDetector = value; }
    private AgiWarmup? _agiWarmup { get => _embodimentSub.AgiWarmup; set => _embodimentSub.AgiWarmup = value; }
    private bool _userWasPresent { get => _embodimentSub.UserWasPresent; set => _embodimentSub.UserWasPresent = value; }
    private DateTime _lastGreetingTime { get => _embodimentSub.LastGreetingTime; set => _embodimentSub.LastGreetingTime = value; }

    // ── Non-subsystem local state ──
    private readonly StringBuilder _currentInputBuffer = new();
    private readonly object _inputLock = new();
    private bool _isInConversationLoop;

    // State
    private bool _isInitialized;
    private bool _disposed;

    // ═══════════════════════════════════════════════════════════════════════════
    // DI SUBSYSTEMS — each manages a cohesive group of capabilities + disposal
    // ═══════════════════════════════════════════════════════════════════════════
    private readonly VoiceSubsystem _voiceSub;
    private readonly ModelSubsystem _modelsSub;
    private readonly ToolSubsystem _toolsSub;
    private readonly MemorySubsystem _memorySub;
    private readonly CognitiveSubsystem _cognitiveSub;
    private readonly AutonomySubsystem _autonomySub;
    private readonly EmbodimentSubsystem _embodimentSub;
    private readonly LocalizationSubsystem _localizationSub;
    private readonly LanguageSubsystem     _languageSub;
    private readonly SelfAssemblySubsystem _selfAssemblySub;
    private readonly PipeProcessingSubsystem _pipeSub;
    private readonly ChatSubsystem _chatSub;
    private readonly CommandRoutingSubsystem _commandRoutingSub;
    private readonly IAgentSubsystem[] _allSubsystems;

    /// <summary>
    /// Gets whether the agent is fully initialized.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Gets the voice service.
    /// </summary>
    public VoiceModeService Voice => _voice;

    /// <summary>
    /// Gets the unified Rx streaming voice service V2.
    /// </summary>
    public VoiceModeServiceV2? VoiceV2 => _voiceV2;

    /// <summary>
    /// Gets the unified Rx interaction stream for all voice events.
    /// Available from VoiceModeService with Rx streaming.
    /// </summary>
    public Ouroboros.Domain.Voice.InteractionStream InteractionStream => _voice.Stream;

    /// <summary>
    /// Gets the agent presence controller for state management and barge-in.
    /// </summary>
    public Ouroboros.Domain.Voice.AgentPresenceController PresenceController => _voice.Presence;

    /// <summary>
    /// Gets the voice side channel for fire-and-forget audio playback.
    /// </summary>
    public VoiceSideChannel? VoiceChannel => _voiceSideChannel;

    /// <summary>
    /// Gets the skill registry.
    /// </summary>
    public ISkillRegistry? Skills => _skills;

    /// <summary>
    /// Gets the personality engine.
    /// </summary>
    public PersonalityEngine? Personality => _personalityEngine;

    // ── Public subsystem access for OuroborosAgentService wiring ──────────────
    public IModelSubsystem    SubModels   => _modelsSub;
    public IToolSubsystem     SubTools    => _toolsSub;
    public IMemorySubsystem   SubMemory   => _memorySub;
    public IAutonomySubsystem SubAutonomy => _autonomySub;
    /// <summary>Iaret — the ImmersivePersona owned by the cognitive subsystem.</summary>
    public ImmersivePersona?  IaretPersona => _immersivePersona;
    /// <summary>
    /// The avatar service owned by EmbodimentSubsystem.
    /// ImmersiveMode uses this to animate the avatar (speaking/listening/idle presence states)
    /// when running alongside OuroborosAgent rather than starting its own avatar instance.
    /// </summary>
    public Application.Avatar.InteractiveAvatarService? AvatarService => _avatarService;
    /// <summary>Language detection subsystem (aya-expanse:8b cloud model).</summary>
    public ILanguageSubsystem SubLanguage => _languageSub;


    /// <summary>
    /// Creates a new Ouroboros agent with DI-injected subsystems.
    /// </summary>
    public OuroborosAgent(
        OuroborosConfig config,
        IVoiceSubsystem voice,
        IModelSubsystem models,
        IToolSubsystem tools,
        IMemorySubsystem memory,
        ICognitiveSubsystem cognitive,
        IAutonomySubsystem autonomy,
        IEmbodimentSubsystem embodiment,
        ILocalizationSubsystem localization,
        ILanguageSubsystem language,
        ISelfAssemblySubsystem selfAssembly,
        IPipeProcessingSubsystem pipeProcessing,
        IChatSubsystem chat,
        ICommandRoutingSubsystem commandRouting)
    {
        _config = config;
        _output = new ConsoleOutput(config.Verbosity);

        _voiceSub = (VoiceSubsystem)voice;
        _modelsSub = (ModelSubsystem)models;
        _toolsSub = (ToolSubsystem)tools;
        _memorySub = (MemorySubsystem)memory;
        _cognitiveSub = (CognitiveSubsystem)cognitive;
        _autonomySub = (AutonomySubsystem)autonomy;
        _embodimentSub = (EmbodimentSubsystem)embodiment;
        _localizationSub = (LocalizationSubsystem)localization;
        _languageSub     = (LanguageSubsystem)language;
        _selfAssemblySub = (SelfAssemblySubsystem)selfAssembly;
        _pipeSub = (PipeProcessingSubsystem)pipeProcessing;
        _chatSub = (ChatSubsystem)chat;
        _commandRoutingSub = (CommandRoutingSubsystem)commandRouting;

        _voice = _voiceSub.Service;
        _allSubsystems =
        [
            _voiceSub, _modelsSub, _toolsSub, _memorySub,
            _cognitiveSub, _autonomySub, _embodimentSub,
            _localizationSub, _languageSub, _selfAssemblySub, _pipeSub, _chatSub, _commandRoutingSub
        ];

        // Register process exit handler to kill speech processes on forceful exit
        AppDomain.CurrentDomain.ProcessExit += (_, _) => VoiceSubsystem.KillAllSpeechProcesses();
        Console.CancelKeyPress += (_, _) => VoiceSubsystem.KillAllSpeechProcesses();
    }

    /// <summary>
    /// Creates a new Ouroboros agent (legacy constructor — creates subsystems internally).
    /// Prefer the DI constructor for testability and modularity.
    /// </summary>
    [Obsolete("Use DI constructor via SubsystemRegistration.AddOuroboros() instead.")]
    public OuroborosAgent(OuroborosConfig config)
        : this(
            config,
            new VoiceSubsystem(new VoiceModeService(new VoiceModeConfig(
                Persona: config.Persona,
                VoiceOnly: config.VoiceOnly,
                LocalTts: config.LocalTts,
                VoiceLoop: true,
                DisableStt: true,
                Model: config.Model,
                Endpoint: config.Endpoint,
                EmbedModel: config.EmbedModel,
                QdrantEndpoint: config.QdrantEndpoint,
                Culture: config.Culture))),
            new ModelSubsystem(),
            new ToolSubsystem(),
            new MemorySubsystem(),
            new CognitiveSubsystem(),
            new AutonomySubsystem(),
            new EmbodimentSubsystem(),
            new LocalizationSubsystem(),
            new LanguageSubsystem(),
            new SelfAssemblySubsystem(),
            new PipeProcessingSubsystem(),
            new ChatSubsystem(),
            new CommandRoutingSubsystem())
    {
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // ── Pre-dispose hooks (cost summary, personality save) ──
        await OnDisposingAsync();

        // ── Dispose all subsystems in reverse registration order ──
        for (int i = _allSubsystems.Length - 1; i >= 0; i--)
        {
            try
            {
                await _allSubsystems[i].DisposeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Dispose] {_allSubsystems[i].Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Pre-dispose hooks: save state, display summaries, and clean up non-subsystem resources.
    /// </summary>
    private async Task OnDisposingAsync()
    {
        // Display cost summary if enabled
        if (_config.CostSummary && _costTracker != null)
        {
            var metrics = _costTracker.GetSessionMetrics();
            if (metrics.TotalRequests > 0)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(_costTracker.FormatSessionSummary());
                Console.ResetColor();
            }
        }

        // Save personality snapshot before shutdown
        if (_personalityEngine != null)
        {
            try
            {
                await _memorySub.SavePersonalitySnapshotAsync(_voice.ActivePersona.Name);
                _output.WriteDebug("Personality snapshot saved");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ Failed to save personality snapshot: {ex.Message}");
            }
        }

        // Clear sub-agents (not owned by a subsystem since the dict is readonly here)
        _subAgents.Clear();
    }



    /// <summary>
    /// Kills all active speech processes (called on dispose and process exit).
    /// Delegates to <see cref="VoiceSubsystem.KillAllSpeechProcesses"/>.
    /// </summary>
    internal static void KillAllSpeechProcesses()
        => VoiceSubsystem.KillAllSpeechProcesses();
}