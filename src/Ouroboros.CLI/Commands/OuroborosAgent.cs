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
    /// Strips tool results from text for voice output.
    /// Tool results like "[tool_name]: output" and "[TOOL-RESULT:...]" are removed.
    /// </summary>
    private static string StripToolResults(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        // Remove lines that match tool result patterns:
        // - [tool_name]: ...
        // - [TOOL-RESULT:tool_name] ...
        // - [propose_intention]: ...
        // - error: ...
        string[] lines = text.Split('\n');
        IEnumerable<string> filtered = lines.Where(line =>
        {
            string trimmed = line.Trim();
            // Skip lines starting with [something]:
            if (Regex.IsMatch(trimmed, @"^\[[\w_:-]+\]:?\s*"))
                return false;
            // Skip lines containing TOOL-RESULT
            if (trimmed.Contains("TOOL-RESULT", StringComparison.OrdinalIgnoreCase))
                return false;
            // Skip error lines
            if (trimmed.StartsWith("error:", StringComparison.OrdinalIgnoreCase))
                return false;
            return true;
        });

        return string.Join("\n", filtered).Trim();
    }

    /// <summary>Uses LLM to integrate tool results naturally into a conversational response.</summary>
    private Task<string> SanitizeToolResultsAsync(string originalResponse, string toolResults)
        => _chatSub.SanitizeToolResultsAsync(originalResponse, toolResults);

    /// <summary>
    /// Speaks text on the voice side channel (fire-and-forget, non-blocking).
    /// Uses the configured persona's voice. Tool results are omitted.
    /// </summary>
    public void Say(string text, string? persona = null)
    {
        if (_voiceSideChannel == null)
        {
            if (_config.Debug) Console.WriteLine("  [VoiceChannel] Not initialized");
            return;
        }

        if (!_voiceSideChannel.IsEnabled)
        {
            if (_config.Debug) Console.WriteLine("  [VoiceChannel] Not enabled (no synthesizer?)");
            return;
        }

        // Strip tool results from voice output
        var cleanText = StripToolResults(text);
        if (string.IsNullOrWhiteSpace(cleanText)) return;

        if (_config.Debug) Console.WriteLine($"  [VoiceChannel] Say: {cleanText[..Math.Min(50, cleanText.Length)]}...");
        _voiceSideChannel.Say(cleanText, persona ?? _config.Persona);
    }

    /// <summary>
    /// Speaks text with a specific persona's voice.
    /// </summary>
    public void SayAs(string persona, string text)
    {
        var cleanText = StripToolResults(text);
        if (!string.IsNullOrWhiteSpace(cleanText))
        {
            _voiceSideChannel?.Say(cleanText, persona);
        }
    }

    /// <summary>
    /// Speaks text and waits for completion (blocking).
    /// </summary>
    public async Task SayAndWaitAsync(string text, string? persona = null, CancellationToken ct = default)
    {
        var cleanText = StripToolResults(text);
        if (string.IsNullOrWhiteSpace(cleanText)) return;
        if (_voiceSideChannel == null) return;

        await _voiceSideChannel.SayAndWaitAsync(cleanText, persona ?? _config.Persona, ct);
    }

    /// <summary>
    /// Announces a system message (high priority).
    /// </summary>
    public void Announce(string text)
    {
        _voiceSideChannel?.Announce(text);
    }

    /// <summary>
    /// Starts listening for voice input using the enhanced listening service.
    /// Supports continuous streaming STT, wake word detection, barge-in, and Whisper fallback.
    /// </summary>
    public async Task StartListeningAsync()
    {
        if (_isListening) return;

        _listeningCts = new CancellationTokenSource();
        _isListening = true;

        _output.WriteSystem(GetLocalizedString("listening_start"));

        // Create the enhanced listening service
        _enhancedListener = new Ouroboros.CLI.Services.EnhancedListeningService(
            _config,
            _output,
            processInput: ChatAsync,
            speak: (text, ct) => SpeakResponseWithAzureTtsAsync(
                text,
                _config.AzureSpeechKey ?? Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY") ?? "",
                _config.AzureSpeechRegion,
                ct));

        _listeningTask = Task.Run(async () =>
        {
            try
            {
                await _enhancedListener.StartAsync(_listeningCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
            catch (Exception ex)
            {
                _output.WriteError($"Listening error: {ex.Message}");
            }
            finally
            {
                _isListening = false;
            }
        });

        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops listening for voice input.
    /// </summary>
    public void StopListening()
    {
        if (!_isListening) return;

        _listeningCts?.Cancel();

        // Dispose the enhanced listener
        if (_enhancedListener != null)
        {
            _enhancedListener.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _enhancedListener = null;
        }

        _isListening = false;
        _output.WriteSystem(GetLocalizedString("listening_stop"));
    }

    /// <summary>
    /// Adds a tool to the registry and refreshes the LLM to use the updated tools.
    /// This ensures dynamically created tools are immediately available for use.
    /// </summary>
    /// <param name="tool">The tool to add.</param>
    private void AddToolAndRefreshLlm(ITool tool)
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

    /// <summary>
    /// Continuous listening loop using Azure Speech Recognition with optional Azure TTS response.
    /// </summary>
    private async Task ListenLoopAsync(CancellationToken ct)
    {
        // Get Azure Speech credentials from environment or static configuration
        string? speechKey = Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY")
                       ?? _staticConfiguration?["Azure:Speech:Key"]
                       ?? _config.AzureSpeechKey;
        string speechRegion = Environment.GetEnvironmentVariable("AZURE_SPEECH_REGION")
                          ?? _staticConfiguration?["Azure:Speech:Region"]
                          ?? _config.AzureSpeechRegion
                          ?? "eastus";

        if (string.IsNullOrEmpty(speechKey))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(GetLocalizedString("voice_requires_key"));
            Console.ResetColor();
            return;
        }

        Microsoft.CognitiveServices.Speech.SpeechConfig config = Microsoft.CognitiveServices.Speech.SpeechConfig.FromSubscription(speechKey, speechRegion);

        // Set speech recognition language based on culture if available
        config.SpeechRecognitionLanguage = _config.Culture ?? "en-US";

        using Microsoft.CognitiveServices.Speech.SpeechRecognizer recognizer = new Microsoft.CognitiveServices.Speech.SpeechRecognizer(config);

        while (!ct.IsCancellationRequested)
        {
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write("  🎤 ");
            Console.ResetColor();

            Microsoft.CognitiveServices.Speech.SpeechRecognitionResult result = await recognizer.RecognizeOnceAsync();

            if (result.Reason == Microsoft.CognitiveServices.Speech.ResultReason.RecognizedSpeech)
            {
                string text = result.Text.Trim();
                if (string.IsNullOrEmpty(text)) continue;

                // Check for stop commands
                if (text.ToLowerInvariant().Contains("stop listening") ||
                    text.ToLowerInvariant().Contains("disable voice"))
                {
                    StopListening();
                    _autonomousCoordinator?.ProcessCommand("/listen off");
                    break;
                }

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  {GetLocalizedString("you_said")} {text}");
                Console.ResetColor();

                // Process as regular input
                string response = await ChatAsync(text);

                // Display response
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n  {response}");
                Console.ResetColor();

                // Speak response using Azure TTS if enabled
                if (_config.AzureTts && !string.IsNullOrEmpty(speechKey))
                {
                    try
                    {
                        await SpeakResponseWithAzureTtsAsync(response, speechKey, speechRegion, ct);
                    }
                    catch (Exception ex)
                    {
                        if (_config.Debug)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.WriteLine($"  ⚠ Azure TTS error: {ex.Message}");
                            Console.ResetColor();
                        }
                    }
                }
            }
            else if (result.Reason == Microsoft.CognitiveServices.Speech.ResultReason.NoMatch)
            {
                // No speech detected, continue listening
            }
            else if (result.Reason == Microsoft.CognitiveServices.Speech.ResultReason.Canceled)
            {
                var cancellation = Microsoft.CognitiveServices.Speech.CancellationDetails.FromResult(result);
                if (cancellation.Reason == Microsoft.CognitiveServices.Speech.CancellationReason.Error)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  ⚠ Speech recognition error: {cancellation.ErrorDetails}");
                    Console.ResetColor();
                }
                break;
            }
        }
    }

    /// <summary>
    /// Speaks a response using Azure TTS with configured voice.
    /// Supports barge-in via the CancellationToken — cancelling stops synthesis immediately.
    /// </summary>
    private async Task SpeakResponseWithAzureTtsAsync(string text, string key, string region, CancellationToken ct)
    {
        try
        {
            var config = Microsoft.CognitiveServices.Speech.SpeechConfig.FromSubscription(key, region);

            // Auto-select voice based on culture (unless user explicitly set a non-default voice)
            var voiceName = GetEffectiveVoice();
            config.SpeechSynthesisVoiceName = voiceName;

            // Use default speaker
            using var speechSynthesizer = new Microsoft.CognitiveServices.Speech.SpeechSynthesizer(config);

            // Register cancellation to stop synthesis (barge-in support)
            ct.Register(() =>
            {
                try { _ = speechSynthesizer.StopSpeakingAsync(); }
                catch { /* Best effort */ }
            });

            // For cross-lingual voices <speak> carries the voice's primary locale,
            // <voice xml:lang> carries the target language.
            var voicePrimaryLocale = voiceName.Length >= 5 ? voiceName[..5] : "en-US";
            var voiceLang = ImmersiveMode.LastDetectedCulture ?? _config.Culture ?? voicePrimaryLocale;
            var ssml = $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='{voicePrimaryLocale}'>
    <voice name='{voiceName}' xml:lang='{voiceLang}'>
        {System.Net.WebUtility.HtmlEncode(text)}
    </voice>
</speak>";

            var result = await speechSynthesizer.SpeakSsmlAsync(ssml);

            if (result.Reason != Microsoft.CognitiveServices.Speech.ResultReason.SynthesizingAudioCompleted)
            {
                if (_config.Debug)
                {
                    _output.WriteDebug($"[Azure TTS] Synthesis issue: {result.Reason}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during barge-in
        }
        catch (Exception ex)
        {
            if (_config.Debug)
            {
                _output.WriteDebug($"[Azure TTS] Error: {ex.Message}");
            }
        }
    }

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
        var agentEventBus = new CLI.Infrastructure.EventBroker<CLI.Infrastructure.AgentEvent>();

        // Create shared initialization context (mediator pattern)
        var ctx = new Subsystems.SubsystemInitContext
        {
            Config = _config,
            Output = _output,
            VoiceService = _voice,
            StaticConfiguration = _staticConfiguration,
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

        // ── Phase 1: Infrastructure (standalone) ──
        if (_config.Voice)
            await _voice.InitializeAsync();

        _voiceSub.SpeakWithSapiFunc = SpeakWithSapiAsync;
        await _voiceSub.InitializeAsync(ctx);

        // ── Phase 2: Models (standalone) ──
        await _modelsSub.InitializeAsync(ctx);

        // ── Phase 2.5: Localization + Language detection (need Config; Language Llm is remote) ──
        await _localizationSub.InitializeAsync(ctx);
        await _languageSub.InitializeAsync(ctx);

        // Wire LLM sanitizer on voice side channel
        if (_config.VoiceChannel && _voiceSideChannel != null && _chatModel != null)
        {
            _voiceSideChannel.SetLlmSanitizer(async (prompt, ct) =>
                await _chatModel.GenerateTextAsync(prompt, ct));
            _output.RecordInit("Voice LLM Sanitizer", true, "natural speech condensation");
        }

        // ── Phase 3: Tools (needs Models) ──
        await _toolsSub.InitializeAsync(ctx);

        // ── Phase 4: Memory (needs Models, uses MeTTa from Tools) ──
        await _memorySub.InitializeAsync(ctx);

        // ── Phase 5: Cognitive (needs Models + Memory + Tools) ──
        await _cognitiveSub.InitializeAsync(ctx);

        // ── Phase 6: Autonomy (needs all above) ──
        await _autonomySub.InitializeAsync(ctx);

        // ── Phase 7: Embodiment (needs Memory + Autonomy) ──
        await _embodimentSub.InitializeAsync(ctx);

        // ── Phase 7.1: SelfAssembly (needs Autonomy; Llm + History wired after Phase 8) ──
        await _selfAssemblySub.InitializeAsync(ctx);

        // ── Phase 7.2: PipeProcessing (needs Config; ProcessInputFunc wired after Phase 8) ──
        await _pipeSub.InitializeAsync(ctx);

        // ── Phase 7.3: Chat (needs all original subsystems) ──
        await _chatSub.InitializeAsync(ctx);

        // ── Phase 7.4: CommandRouting (needs Tools, Memory, Autonomy) ──
        await _commandRoutingSub.InitializeAsync(ctx);

        // ── Phase 8: Cross-subsystem wiring (mediator orchestration) ──
        WireCrossSubsystemDependencies();

        // ── Phase 9: Post-init actions ──
        _isInitialized = true;
        _output.FlushInitSummary();
        if (_config.Verbosity != OutputVerbosity.Quiet)
        {
            Console.WriteLine("\n  ✓ Ouroboros fully initialized\n");
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
    /// This is the core of the mediator pattern — connecting subsystem components.
    /// </summary>
    private void WireCrossSubsystemDependencies()
    {
        // ── Autonomy subsystem cross-references ──
        WireAutonomyCallbacks();

        // ── Autonomous Mind delegates ──
        WireAutonomousMindDelegatesAsync().GetAwaiter().GetResult();

        // ── Autonomous Coordinator ──
        WireAutonomousCoordinatorAsync().GetAwaiter().GetResult();

        // ── Self-Execution ──
        if (_config.EnableMind)
            WireSelfExecution();

        // ── Push Mode ──
        if (_config.EnablePush)
            WirePushMode();

        // ── Presence Detection events ──
        WirePresenceDetection();

        // -- Persona events --
        WirePersonaEvents();

        // -- SystemAccessTools shared state --
        WireSystemAccessTools();

        // -- Network state persistence delegates --
        WireNetworkPersistence();

        // -- Avatar mood transitions --
        WireAvatarMoodTransitions();

        // -- Pipeline think delegate --
        WirePipelineThinkDelegate();

        // -- Memory subsystem thought persistence --
        _memorySub.PersistThoughtFunc = PersistThoughtAsync;

        // ── Localization: wire LLM for thought translation ──
        _localizationSub.Llm = _llm;

        // ── SelfAssembly: wire LLM, conversation history, and event callbacks ──
        _selfAssemblySub.Llm = _llm;
        _selfAssemblySub.ConversationHistory = _memorySub.ConversationHistory;
        _selfAssemblySub.WireCallbacks();

        // ── PipeProcessing: wire the central input dispatch function ──
        _pipeSub.ProcessInputFunc = ProcessInputAsync;

        // ── Chat: wire persistence delegates and language lookup ──
        _chatSub.PersistThoughtFunc = PersistThoughtAsync;
        _chatSub.PersistThoughtResultFunc = (id, type, content, success, confidence) =>
            PersistThoughtResultAsync(id, type, content, success, confidence);
        _chatSub.GetLanguageNameFunc = _localizationSub.GetLanguageName;
    }

    /// <summary>
    /// Wires agent-level callbacks into the autonomy subsystem so it can access
    /// cross-cutting concerns (voice, chat, conversation state).
    /// </summary>
    private void WireAutonomyCallbacks()
    {
        _autonomySub.IsInConversationLoop = () => _isInConversationLoop;
        _autonomySub.SayAndWaitAsyncFunc = (text, persona) => SayAndWaitAsync(text, persona);
        _autonomySub.AnnounceAction = Announce;
        _autonomySub.ChatAsyncFunc = ChatAsync;
        _autonomySub.GetLanguageNameFunc = GetLanguageName;
        _autonomySub.StartListeningAsyncFunc = () => StartListeningAsync();
        _autonomySub.StopListeningAction = StopListening;
    }

    /// <summary>
    /// Wires AutonomousMind's ThinkFunction, SearchFunction, ExecuteToolFunction,
    /// pipe command execution, output sanitization, and all event handlers.
    /// </summary>
    private async Task WireAutonomousMindDelegatesAsync()
    {
        if (_autonomousMind == null) return;

        _autonomousMind.ThinkFunction = async (prompt, token) =>
        {
            var actualPrompt = prompt;
            if (!string.IsNullOrEmpty(_config.Culture) && _config.Culture != "en-US")
            {
                var languageName = GetLanguageName(_config.Culture);
                actualPrompt = $"LANGUAGE: Respond ONLY in {languageName}. No English.\n\n{prompt}";
            }
            return await GenerateWithOrchestrationAsync(actualPrompt, token);
        };

        _autonomousMind.SearchFunction = async (query, token) =>
        {
            var searchTool = _toolFactory?.CreateWebSearchTool("duckduckgo");
            if (searchTool != null)
            {
                var result = await searchTool.InvokeAsync(query, token);
                return result.Match(s => s, _ => "");
            }
            return "";
        };

        _autonomousMind.ExecuteToolFunction = async (toolName, input, token) =>
        {
            var tool = _tools.Get(toolName);
            if (tool != null)
            {
                var result = await tool.InvokeAsync(input, token);
                return result.Match(s => s, e => $"Error: {e}");
            }
            return "Tool not found";
        };

        // Register subsystem instances in the Scrutor-backed IServiceProvider
        // so ServiceDiscoveryTool can list and invoke them at runtime.
        Ouroboros.Application.Tools.ServiceContainerFactory.RegisterSingleton(_autonomousMind);

        // Seed baseline interests so autonomous learning keeps research/philosophy active.
        _autonomousMind.AddInterest("research");
        _autonomousMind.AddInterest("philosophy");
        _autonomousMind.AddInterest("epistemology");
        _autonomousMind.AddInterest("self-improvement");

        _autonomousMind.VerifyFileExistsFunction = (path) =>
        {
            var absolutePath = Path.IsPathRooted(path) ? path : Path.Combine(Environment.CurrentDirectory, path);
            return File.Exists(absolutePath);
        };

        _autonomousMind.ComputeFileHashFunction = (path) =>
        {
            try
            {
                var absolutePath = Path.IsPathRooted(path) ? path : Path.Combine(Environment.CurrentDirectory, path);
                if (!File.Exists(absolutePath)) return null;
                using var stream = File.OpenRead(absolutePath);
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var hash = sha256.ComputeHash(stream);
                return Convert.ToBase64String(hash);
            }
            catch { return null; }
        };

        _autonomousMind.ExecutePipeCommandFunction = async (pipeCommand, token) =>
        {
            try { return await ProcessInputWithPipingAsync(pipeCommand); }
            catch (Exception ex) { return $"Pipe execution failed: {ex.Message}"; }
        };

        _autonomousMind.SanitizeOutputFunction = async (rawOutput, token) =>
        {
            if (_chatModel == null || string.IsNullOrWhiteSpace(rawOutput))
                return rawOutput;
            try
            {
                string prompt = PromptResources.SummarizeToolOutput(rawOutput);
                string sanitized = await _chatModel.GenerateTextAsync(prompt, token);
                return string.IsNullOrWhiteSpace(sanitized) ? rawOutput : sanitized.Trim();
            }
            catch { return rawOutput; }
        };

        // Wire limitation-busting tools
        AutonomousTools.VerifyClaimTool.SearchFunction = _autonomousMind.SearchFunction;
        AutonomousTools.VerifyClaimTool.EvaluateFunction = async (prompt, token) =>
            _chatModel != null ? await _chatModel.GenerateTextAsync(prompt, token) : "";
        AutonomousTools.ReasoningChainTool.ReasonFunction = async (prompt, token) =>
            _chatModel != null ? await _chatModel.GenerateTextAsync(prompt, token) : "";
        AutonomousTools.ParallelToolsTool.ExecuteToolFunction = _autonomousMind.ExecuteToolFunction;
        AutonomousTools.CompressContextTool.SummarizeFunction = async (prompt, token) =>
            _chatModel != null ? await _chatModel.GenerateTextAsync(prompt, token) : "";
        AutonomousTools.SelfDoubtTool.CritiqueFunction = async (prompt, token) =>
            _chatModel != null ? await _chatModel.GenerateTextAsync(prompt, token) : "";
        AutonomousTools.ParallelMeTTaThinkTool.OllamaFunction = async (prompt, token) =>
            _chatModel != null ? await _chatModel.GenerateTextAsync(prompt, token) : "";
        AutonomousTools.OuroborosMeTTaTool.OllamaFunction = async (prompt, token) =>
            _chatModel != null ? await _chatModel.GenerateTextAsync(prompt, token) : "";

        // Proactive message events — track and whisper only; don't display in console
        _autonomousMind.OnProactiveMessage += async (msg) =>
        {
            var thoughtContent = msg.TrimStart();
            if (thoughtContent.StartsWith("💡") || thoughtContent.StartsWith("💬") ||
                thoughtContent.StartsWith("🤔") || thoughtContent.StartsWith("💭"))
                thoughtContent = thoughtContent[2..].Trim();
            TrackLastThought(thoughtContent);

            // Push the proactive thought as Iaret's StatusText — these are the most
            // conversation-contextual LLM-generated thoughts (research findings, feelings).
            if (_avatarService is { } svc)
                svc.NotifyMoodChange(svc.CurrentState.Mood, svc.CurrentState.Energy, svc.CurrentState.Positivity, statusText: thoughtContent);

            try { await _voice.WhisperAsync(msg); } catch { }
        };

        // Display only research (Curiosity/Observation) and inner feelings (Reflection) thoughts.
        // Algorithmic action/strategic/sharing thoughts are suppressed.
        _autonomousMind.OnThought += async (thought) =>
        {
            System.Diagnostics.Debug.WriteLine($"[Thought] {thought.Type}: {thought.Content}");
            var thoughtType = thought.Type switch
            {
                Ouroboros.Application.Services.ThoughtType.Reflection => InnerThoughtType.SelfReflection,
                Ouroboros.Application.Services.ThoughtType.Curiosity => InnerThoughtType.Curiosity,
                Ouroboros.Application.Services.ThoughtType.Observation => InnerThoughtType.Observation,
                Ouroboros.Application.Services.ThoughtType.Creative => InnerThoughtType.Creative,
                Ouroboros.Application.Services.ThoughtType.Sharing => InnerThoughtType.Synthesis,
                Ouroboros.Application.Services.ThoughtType.Action => InnerThoughtType.Strategic,
                _ => InnerThoughtType.Wandering
            };
            var innerThought = InnerThought.CreateAutonomous(thoughtType, thought.Content, confidence: 0.7);

            // Only surface research and emotional thoughts; suppress strategic/action/synthesis noise
            bool isResearch = thought.Type is
                Ouroboros.Application.Services.ThoughtType.Curiosity or
                Ouroboros.Application.Services.ThoughtType.Observation;
            bool isFeeling = thought.Type is Ouroboros.Application.Services.ThoughtType.Reflection;

            if (isResearch || isFeeling)
            {
                if (_config.Verbosity != OutputVerbosity.Quiet)
                {
                    Console.ForegroundColor = ConsoleColor.DarkMagenta;
                    Console.WriteLine($"\n  💭 {thought.Content}");
                    Console.ResetColor();
                }

                // Push the real thought as Iaret's StatusText
                if (_avatarService is { } svc)
                    svc.NotifyMoodChange(svc.CurrentState.Mood, svc.CurrentState.Energy, svc.CurrentState.Positivity, statusText: thought.Content);
            }

            await PersistThoughtAsync(innerThought, "autonomous_thinking");
        };

        // InnerDialogEngine (algorithmic thought generation) intentionally not connected.
    }

    /// <summary>
    /// Wires AutonomousCoordinator with tool execution, thinking, embedding,
    /// Qdrant storage, MeTTa reasoning, chat processing, and voice control.
    /// </summary>
    private async Task WireAutonomousCoordinatorAsync()
    {
        if (_autonomousCoordinator == null)
        {
            try
            {
                await InitializeAutonomousCoordinatorAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ Autonomous Coordinator wiring failed: {ex.Message}");
            }
            return;
        }
        // Coordinator was created by AutonomySubsystem; just wire delegates here
    }

    /// <summary>
    /// Wires self-execution background loop.
    /// </summary>
    private void WireSelfExecution()
    {
        _selfExecutionCts?.Dispose();
        _selfExecutionCts = new CancellationTokenSource();
        _selfExecutionEnabled = true;
        _selfExecutionTask = Task.Run(_autonomySub.SelfExecutionLoopAsync, _selfExecutionCts.Token);
        _output.RecordInit("Self-Execution", true, "autonomous goal pursuit");
    }


    /// <summary>
    /// Starts push mode by activating the coordinator tick loop.
    /// </summary>
    private void WirePushMode()
    {
        if (_autonomousCoordinator == null)
        {
            _output.WriteWarning("Cannot start Push Mode: Coordinator not initialized");
            return;
        }
        try
        {
            _autonomousCoordinator.Start();
            _pushModeCts?.Dispose();
            _pushModeCts = new CancellationTokenSource();
            _pushModeTask = Task.Run(() => _autonomySub.PushModeLoopAsync(_pushModeCts.Token), _pushModeCts.Token);
            var yoloPart = _config.YoloMode ? ", YOLO" : "";
            _output.RecordInit("Push Mode", true, $"interval: {_config.IntentionIntervalSeconds}s{yoloPart}");
        }
        catch (Exception ex)
        {
            _output.WriteWarning($"Push Mode start failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Wires presence detector events (HandlePresenceDetectedAsync, absence tracking).
    /// </summary>
    private void WirePresenceDetection()
    {
        if (_presenceDetector == null) return;

        _presenceDetector.OnPresenceDetected += async evt =>
        {
            await HandlePresenceDetectedAsync(evt);
        };

        _presenceDetector.OnAbsenceDetected += evt =>
        {
            _userWasPresent = false;
            System.Diagnostics.Debug.WriteLine($"[Presence] User absence detected via {evt.Source}");
        };
    }

        /// <summary>
    /// Wires ImmersivePersona events (AutonomousThought, ConsciousnessShift).
    /// </summary>
    private void WirePersonaEvents()
    {
        if (_immersivePersona == null) return;

        // ImmersivePersona autonomous thoughts — surface research/feelings, skip pure templates.
        // Metacognitive and Musing types come from InnerDialogEngine string templates
        // ("I notice that I tend to {0}") and are not genuine LLM thoughts — skip those.
        _immersivePersona.AutonomousThought += (_, e) =>
        {
            if (e.Thought.Type is not (InnerThoughtType.Curiosity or InnerThoughtType.Observation or InnerThoughtType.SelfReflection))
                return;

            var content = e.Thought.Content;

            // Filter: skip empty, very short, or unresolved template placeholders.
            // Template artifacts contain bracket-enclosed tags like "[Symbolic context: ...]"
            // that were never substituted with real content.
            if (string.IsNullOrWhiteSpace(content) || content.Length < 12)
                return;
            var bracketIdx = content.IndexOf('[');
            if (bracketIdx >= 0 && content.IndexOf(':', bracketIdx) > bracketIdx)
                return;

            // NOTE: when ImmersiveMode is running, ImmersiveSubsystem.WirePersonaEvents also
            // subscribes to this persona and will print the same thought within milliseconds.
            // ImmersiveSubsystem's dedup guard (8 s window) suppresses the duplicate print.
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine($"\n  💭 {content}");
            Console.ResetColor();

            // Push genuine persona thoughts to avatar — excludes Metacognitive/Musing
            // templates which are filled from topic keywords, not LLM generation.
            if (_avatarService is { } svc)
                svc.NotifyMoodChange(svc.CurrentState.Mood, svc.CurrentState.Energy, svc.CurrentState.Positivity, statusText: content);
        };
    }

    /// <summary>
    /// Wires SystemAccessTools shared static state (SharedPersistence, SharedMind, SharedIndexer).
    /// </summary>
    private void WireSystemAccessTools()
    {
        if (_selfPersistence != null && _autonomousMind != null)
        {
            SystemAccessTools.SharedPersistence = _selfPersistence;
            SystemAccessTools.SharedMind = _autonomousMind;
        }

        if (_selfIndexer != null)
        {
            SystemAccessTools.SharedIndexer = _selfIndexer;
            _selfIndexer.OnFileIndexed += (file, chunks) =>
                _output.WriteDebug($"[Index] {System.IO.Path.GetFileName(file)} ({chunks} chunks)");
        }
    }

    /// <summary>
    /// Wires AutonomousMind PersistLearningFunction and PersistEmotionFunction
    /// to the PersistentNetworkStateProjector.
    /// </summary>
    private void WireNetworkPersistence()
    {
        if (_autonomousMind == null) return;

        _autonomousMind.PersistLearningFunction = async (category, content, confidence, token) =>
        {
            if (_networkProjector != null)
            {
                await _networkProjector.RecordLearningAsync(
                    category,
                    content,
                    "autonomous_mind",
                    confidence,
                    token);
            }
        };

        _autonomousMind.PersistEmotionFunction = async (emotion, token) =>
        {
            if (_networkProjector != null)
            {
                await _networkProjector.RecordLearningAsync(
                    "emotional_state",
                    $"Emotion: {emotion.DominantEmotion} (arousal={emotion.Arousal:F2}, valence={emotion.Valence:F2}) - {emotion.Description}",
                    "autonomous_mind",
                    0.6,
                    token);
            }
        };

        // Additional events for debugging/diagnostics
        _autonomousMind.OnDiscovery += async (query, fact) =>
        {
            System.Diagnostics.Debug.WriteLine($"[Discovery] {query}: {fact}");
            if (_config.Verbosity != OutputVerbosity.Quiet)
            {
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.WriteLine($"  💭 [inner thought] I just learned from '{query}': {fact}");
                Console.ResetColor();
            }

            var discoveryThought = InnerThought.CreateAutonomous(
                InnerThoughtType.Consolidation,
                $"Discovered: {fact} (from query: {query})",
                confidence: 0.8);
            await PersistThoughtAsync(discoveryThought, "discovery");
        };

        _autonomousMind.OnEmotionalChange += (emotion) =>
        {
            _output.WriteDebug($"[mind] Emotional shift: {emotion.DominantEmotion} ({emotion.Description})");
        };

        _autonomousMind.OnStatePersisted += (msg) =>
        {
            System.Diagnostics.Debug.WriteLine($"[State] {msg}");
        };
    }

    /// <summary>
    /// Wires consciousness shift events to the avatar service for mood transitions.
    /// </summary>
    private void WireAvatarMoodTransitions()
    {
        if (_immersivePersona == null || _avatarService == null) return;

        _immersivePersona.ConsciousnessShift += (_, e) =>
        {
            _avatarService?.NotifyMoodChange(
                e.NewEmotion ?? "neutral",
                0.5 + (e.ArousalChange * 0.5),
                e.NewEmotion?.Contains("warm") == true || e.NewEmotion?.Contains("gentle") == true ? 0.8 : 0.5);
        };
    }

    /// <summary>
    /// Wires AutonomousMind PipelineThinkFunction for monadic reasoning with branch tracking.
    /// </summary>
    private void WirePipelineThinkDelegate()
    {
        if (_autonomousMind == null) return;

        _autonomousMind.PipelineThinkFunction = async (prompt, existingBranch, token) =>
        {
            var response = await GenerateWithOrchestrationAsync(prompt, token);

            if (existingBranch != null)
            {
                var updatedBranch = existingBranch.WithReasoning(
                    new Ouroboros.Domain.States.Thinking(response),
                    prompt,
                    null);
                return (response, updatedBranch);
            }

            return (response, existingBranch!);
        };
    }
/// <summary>
    /// Enforce governance policies when self-modification is enabled.
    /// </summary>
    private async Task EnforceGovernancePoliciesAsync()
    {
        try
        {
            _output.WriteDebug("Enforcing governance policies...");

            var policyOpts = new PolicyOptions
            {
                Command = "enforce",
                Culture = _config.Culture,
                EnableSelfModification = true,
                RiskLevel = _config.RiskLevel,
                AutoApproveLow = _config.AutoApproveLow,
                Verbose = _config.Debug
            };

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await PolicyCommands.RunPolicyAsync(policyOpts);
                    var output = writer.ToString();

                    Console.SetOut(originalOut);
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        _output.WriteDebug(output.Trim());
                    }
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        catch (Exception ex)
        {
            _output.WriteWarning($"Policy enforcement: {ex.Message}");
        }
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

    private void PrintFeatureStatus()
    {
        Console.WriteLine("  Configuration:");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"    Model: {_config.Model}");
        Console.WriteLine($"    Persona: {_config.Persona}");
        var ttsMode = _config.AzureTts ? "✓ Azure (cloud)" : "○ Local (Windows)";
        Console.WriteLine($"    Voice: {(_config.Voice ? "✓ enabled" : "○ disabled")} - {ttsMode}");
        Console.ResetColor();
        Console.WriteLine();

        Console.WriteLine("  Features (all enabled by default, use --no-X to disable):");
        Console.ForegroundColor = _config.EnableSkills ? ConsoleColor.Green : ConsoleColor.DarkGray;
        Console.WriteLine($"    {(_config.EnableSkills ? "✓" : "○")} Skills       - Persistent learning with Qdrant");
        Console.ForegroundColor = _config.EnableMeTTa ? ConsoleColor.Green : ConsoleColor.DarkGray;
        Console.WriteLine($"    {(_config.EnableMeTTa ? "✓" : "○")} MeTTa        - Symbolic reasoning engine");
        Console.ForegroundColor = _config.EnableTools ? ConsoleColor.Green : ConsoleColor.DarkGray;
        Console.WriteLine($"    {(_config.EnableTools ? "✓" : "○")} Tools        - Web search, calculator, URL fetch");
        Console.ForegroundColor = _config.EnableBrowser ? ConsoleColor.Green : ConsoleColor.DarkGray;
        Console.WriteLine($"    {(_config.EnableBrowser ? "✓" : "○")} Browser      - Playwright automation");
        Console.ForegroundColor = _config.EnablePersonality ? ConsoleColor.Green : ConsoleColor.DarkGray;
        Console.WriteLine($"    {(_config.EnablePersonality ? "✓" : "○")} Personality  - Affective states & traits");
        Console.ForegroundColor = _config.EnableMind ? ConsoleColor.Green : ConsoleColor.DarkGray;
        Console.WriteLine($"    {(_config.EnableMind ? "✓" : "○")} Mind         - Autonomous inner thoughts");
        Console.ForegroundColor = _config.EnableConsciousness ? ConsoleColor.Green : ConsoleColor.DarkGray;
        Console.WriteLine($"    {(_config.EnableConsciousness ? "✓" : "○")} Consciousness- ImmersivePersona self-awareness");
        Console.ForegroundColor = _config.EnableEmbodiment ? ConsoleColor.Green : ConsoleColor.DarkGray;
        Console.WriteLine($"    {(_config.EnableEmbodiment ? "✓" : "○")} Embodiment   - Multimodal sensors & actuators");
        Console.ForegroundColor = _config.EnablePush ? ConsoleColor.Cyan : ConsoleColor.DarkGray;
        Console.WriteLine($"    {(_config.EnablePush ? "⚡" : "○")} Push Mode    - Propose actions for approval (--push)");
        Console.ResetColor();
        Console.WriteLine();
    }

    private void PrintQuickHelp()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  Quick commands: 'help' | 'status' | 'skills' | 'tools' | 'exit'");
        Console.WriteLine("  Say or type anything to chat. Use [TOOL:name args] to call tools.\n");
        Console.ResetColor();
    }

    private static async Task SpeakWithSapiAsync(string text, PersonaVoice voice, CancellationToken ct)
    {
        // Try Azure TTS first (higher quality, Cortana-like voices)
        // Check user secrets first, then environment variables
        var azureKey = _staticConfiguration?["Azure:Speech:Key"]
            ?? Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
        var azureRegion = _staticConfiguration?["Azure:Speech:Region"]
            ?? Environment.GetEnvironmentVariable("AZURE_SPEECH_REGION");

        if (!string.IsNullOrEmpty(azureKey) && !string.IsNullOrEmpty(azureRegion))
        {
            if (await SpeakWithAzureTtsAsync(text, voice, azureKey, azureRegion, ct))
                return;
        }

        // Fallback to Windows SAPI
        await SpeakWithWindowsSapiAsync(text, voice, ct);
    }

    private static async Task<bool> SpeakWithAzureTtsAsync(string text, PersonaVoice voice, string key, string region, CancellationToken ct)
    {
        try
        {
            Console.WriteLine($"  [Azure TTS] Speaking as {voice.PersonaName}: {text[..Math.Min(40, text.Length)]}...");

            var config = Microsoft.CognitiveServices.Speech.SpeechConfig.FromSubscription(key, region);

            // Check if culture override is set
            var culture = _staticCulture ?? "en-US";
            var isGerman = culture.Equals("de-DE", StringComparison.OrdinalIgnoreCase);

            // Select Azure Neural voice based on culture and persona
            string azureVoice;
            if (isGerman)
            {
                // German voices for all personas
                azureVoice = voice.PersonaName.ToUpperInvariant() switch
                {
                    "OUROBOROS" => "de-DE-KatjaNeural",   // German female (Cortana-like)
                    "ARIA" => "de-DE-AmalaNeural",        // German expressive female
                    "ECHO" => "de-AT-IngridNeural",       // Austrian German female
                    "SAGE" => "de-DE-KatjaNeural",        // German calm female
                    "ATLAS" => "de-DE-ConradNeural",      // German male
                    "SYSTEM" => "de-DE-KatjaNeural",      // System messages
                    "USER" => "de-DE-ConradNeural",       // User persona - male
                    "USER_PERSONA" => "de-DE-ConradNeural",
                    _ => "de-DE-KatjaNeural"
                };
            }
            else
            {
                // English voices (default)
                azureVoice = voice.PersonaName.ToUpperInvariant() switch
                {
                    "OUROBOROS" => "en-US-JennyNeural",    // Cortana-like voice!
                    "ARIA" => "en-US-AriaNeural",          // Expressive female
                    "ECHO" => "en-GB-SoniaNeural",         // UK female
                    "SAGE" => "en-US-SaraNeural",          // Calm female
                    "ATLAS" => "en-US-GuyNeural",          // Male
                    "SYSTEM" => "en-US-JennyNeural",       // System messages
                    "USER" => "en-US-GuyNeural",           // User persona - male (distinct from Jenny)
                    "USER_PERSONA" => "en-US-GuyNeural",
                    _ => "en-US-JennyNeural"
                };
            }

            config.SpeechSynthesisVoiceName = azureVoice;

            // Use mythic SSML styling for Cortana-like voices (Jenny or Katja)
            var useFriendlyStyle = azureVoice.Contains("Jenny") || azureVoice.Contains("Katja");
            var azureVoicePrimaryLocale = azureVoice.Length >= 5 ? azureVoice[..5] : culture;
            var ssml = useFriendlyStyle
                ? $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis'
                    xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='{azureVoicePrimaryLocale}'>
                    <voice name='{azureVoice}' xml:lang='{culture}'>
                        <mstts:express-as style='friendly' styledegree='0.8'>
                            <prosody rate='-5%' pitch='+8%' volume='+3%'>
                                <mstts:audioduration value='1.1'/>
                                {System.Security.SecurityElement.Escape(text)}
                            </prosody>
                        </mstts:express-as>
                        <mstts:audioeffect type='eq_car'/>
                    </voice>
                </speak>"
                : $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='{azureVoicePrimaryLocale}'>
                    <voice name='{azureVoice}' xml:lang='{culture}'>
                        <prosody rate='0%'>{System.Security.SecurityElement.Escape(text)}</prosody>
                    </voice>
                </speak>";

            using var synthesizer = new Microsoft.CognitiveServices.Speech.SpeechSynthesizer(config);

            var result = await synthesizer.SpeakSsmlAsync(ssml);

            if (result.Reason == Microsoft.CognitiveServices.Speech.ResultReason.SynthesizingAudioCompleted)
            {
                Console.WriteLine($"  [Azure TTS] Done");
                return true;
            }

            if (result.Reason == Microsoft.CognitiveServices.Speech.ResultReason.Canceled)
            {
                var cancellation = Microsoft.CognitiveServices.Speech.SpeechSynthesisCancellationDetails.FromResult(result);
                Console.WriteLine($"  [Azure TTS Error] {cancellation.ErrorDetails}");
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [Azure TTS Exception] {ex.Message}");
            return false; // Fall back to SAPI
        }
    }

    private static async Task SpeakWithWindowsSapiAsync(string text, PersonaVoice voice, CancellationToken ct)
    {
        try
        {
            // Use Windows Speech via PowerShell with persona-specific rate/pitch
            var escapedText = text
                .Replace("'", "''")
                .Replace("\r", " ")
                .Replace("\n", " ");

            // Convert persona rate (0.5-1.5) to SAPI rate (-5 to +5)
            var rate = (int)((voice.Rate - 1.0f) * 10);

            // Select voice based on persona - use different voices for variety
            // Available voices depend on system - check with GetInstalledVoices()
            // Common: Microsoft David (male), Microsoft Zira (female), Microsoft Hedda (German female)
            var voiceSelector = voice.PersonaName.ToUpperInvariant() switch
            {
                "OUROBOROS" => "'Zira'",     // Default: Zira (US female) - closest to Cortana available
                "ARIA" => "'Zira'",          // Female voice
                "ECHO" => "'Hazel'",         // UK female
                "SAGE" => "'Hedda'",         // German female
                "ATLAS" => "'David'",        // David with rate adjustment
                "SYSTEM" => "'Zira'",        // System announcements
                "USER" => "'David'",         // User persona - David (US male, distinct from Zira)
                "USER_PERSONA" => "'David'", // User persona alternate key
                _ => "'Zira'"                // Default fallback
            };

            var script = $@"
Add-Type -AssemblyName System.Speech
$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
$voices = $synth.GetInstalledVoices() | Where-Object {{ $_.VoiceInfo.Culture.Name -like 'en-*' }}
$targetNames = @({voiceSelector})
$selectedVoice = $null
foreach ($target in $targetNames) {{
    $match = $voices | Where-Object {{ $_.VoiceInfo.Name -like ""*$target*"" }} | Select-Object -First 1
    if ($match) {{ $selectedVoice = $match; break }}
}}
if ($selectedVoice) {{ $synth.SelectVoice($selectedVoice.VoiceInfo.Name) }}
elseif ($voices.Count -gt 0) {{ $synth.SelectVoice($voices[0].VoiceInfo.Name) }}
$synth.Rate = {Math.Clamp(rate, -10, 10)}
$synth.Volume = {voice.Volume}
$synth.Speak('{escapedText}')
$synth.Dispose()
";
            using var process = new System.Diagnostics.Process();
            process.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"{script.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            process.Start();

            // Track the process so we can kill it on exit
            VoiceSubsystem.TrackSpeechProcess(process);

            try
            {
                await process.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                // Kill the process if cancelled
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                throw;
            }
            finally
            {
                // Remove from tracking (best effort - ConcurrentBag doesn't have Remove)
            }
        }
        catch
        {
            // Silently fail if SAPI not available
        }
    }


    /// <summary>
    /// Persists a new thought to storage for future sessions.
    /// Uses neuro-symbolic relations when Qdrant is available.
    /// </summary>
    private async Task PersistThoughtAsync(InnerThought thought, string? topic = null)
    {
        if (_thoughtPersistence == null) return;

        try
        {
            // Try to use neuro-symbolic persistence with automatic relation inference
            var neuroStore = _thoughtPersistence.AsNeuroSymbolicStore();
            if (neuroStore != null)
            {
                var sessionId = $"ouroboros-{_config.Persona.ToLowerInvariant()}";
                var persisted = ToPersistedThought(thought, topic);
                await neuroStore.SaveWithRelationsAsync(sessionId, persisted, autoInferRelations: true);
            }
            else
            {
                await _thoughtPersistence.SaveAsync(thought, topic);
            }

            _persistentThoughts.Add(thought);

            // Keep only the most recent 100 thoughts in memory
            if (_persistentThoughts.Count > 100)
            {
                _persistentThoughts.RemoveAt(0);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ThoughtPersistence] Failed to save: {ex.Message}");
        }
    }

    /// <summary>
    /// Persists the result of a thought execution (action taken, response generated, etc).
    /// </summary>
    private async Task PersistThoughtResultAsync(
        Guid thoughtId,
        string resultType,
        string content,
        bool success,
        double confidence,
        TimeSpan? executionTime = null)
    {
        if (_thoughtPersistence == null) return;

        var neuroStore = _thoughtPersistence.AsNeuroSymbolicStore();
        if (neuroStore == null) return;

        try
        {
            var sessionId = $"ouroboros-{_config.Persona.ToLowerInvariant()}";
            var result = new Ouroboros.Domain.Persistence.ThoughtResult(
                Id: Guid.NewGuid(),
                ThoughtId: thoughtId,
                ResultType: resultType,
                Content: content,
                Success: success,
                Confidence: confidence,
                CreatedAt: DateTime.UtcNow,
                ExecutionTime: executionTime);

            await neuroStore.SaveResultAsync(sessionId, result);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ThoughtResult] Failed to save: {ex.Message}");
        }
    }

    /// <summary>
    /// Converts an InnerThought to a PersistedThought.
    /// </summary>
    private static Ouroboros.Domain.Persistence.PersistedThought ToPersistedThought(InnerThought thought, string? topic)
    {
        string? metadataJson = null;
        if (thought.Metadata != null && thought.Metadata.Count > 0)
        {
            try
            {
                metadataJson = System.Text.Json.JsonSerializer.Serialize(thought.Metadata);
            }
            catch
            {
                // Ignore
            }
        }

        return new Ouroboros.Domain.Persistence.PersistedThought
        {
            Id = thought.Id,
            Type = thought.Type.ToString(),
            Content = thought.Content,
            Confidence = thought.Confidence,
            Relevance = thought.Relevance,
            Timestamp = thought.Timestamp,
            Origin = thought.Origin.ToString(),
            Priority = thought.Priority.ToString(),
            ParentThoughtId = thought.ParentThoughtId,
            TriggeringTrait = thought.TriggeringTrait,
            Topic = topic,
            Tags = thought.Tags,
            MetadataJson = metadataJson,
        };
    }


    /// <summary>
    /// Handles presence detection - greets user proactively if push mode enabled.
    /// </summary>
    private async Task HandlePresenceDetectedAsync(PresenceEvent evt)
    {
        System.Diagnostics.Debug.WriteLine($"[Presence] User presence detected via {evt.Source} (confidence={evt.Confidence:P0})");

        // Only proactively greet if:
        // 1. Push mode is enabled
        // 2. User was previously absent (state changed)
        // 3. Haven't greeted recently (avoid spam)
        var shouldGreet = _config.EnablePush &&
                          !_userWasPresent &&
                          (DateTime.UtcNow - _lastGreetingTime).TotalMinutes > 5 &&
                          evt.Confidence > 0.6;

        _userWasPresent = true;

        if (shouldGreet)
        {
            _lastGreetingTime = DateTime.UtcNow;

            // Generate a contextual greeting
            var greeting = await GeneratePresenceGreetingAsync(evt);

            // Notify via AutonomousMind's proactive channel
            if (_autonomousMind != null && !_autonomousMind.SuppressProactiveMessages)
            {
                // Fire proactive message event
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  👋 {greeting}");
                Console.ResetColor();

                // Speak the greeting
                await _voice.WhisperAsync(greeting);

                // If in conversation loop, restore prompt
                if (_isInConversationLoop)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("\n  You: ");
                    Console.ResetColor();
                }
            }
        }
    }

    /// <summary>
    /// Generates a contextual greeting when user presence is detected.
    /// </summary>
    private async Task<string> GeneratePresenceGreetingAsync(PresenceEvent evt)
    {
        var defaultGreeting = GetLocalizedString("Welcome back! I'm here if you need anything.");

        if (_chatModel == null)
        {
            return defaultGreeting;
        }

        try
        {
            var context = evt.TimeSinceLastState.HasValue
                ? $"The user was away for {evt.TimeSinceLastState.Value.TotalMinutes:F0} minutes."
                : "The user just arrived.";

            // Add language directive if culture is set
            var languageDirective = GetLanguageDirective();

            var prompt = PromptResources.GreetingGeneration(languageDirective, context);

            var greeting = await _chatModel.GenerateTextAsync(prompt, CancellationToken.None);
            return greeting?.Trim() ?? defaultGreeting;
        }
        catch
        {
            return defaultGreeting;
        }
    }

    /// <summary>
    /// Performs AGI warmup at startup - primes the model with examples for autonomous operation.
    /// </summary>
    private async Task PerformAgiWarmupAsync()
    {
        try
        {
            if (_config.Verbosity != OutputVerbosity.Quiet)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("\n  ⏳ Warming up AGI systems...");
                Console.ResetColor();
            }

            _agiWarmup = new AgiWarmup(
                thinkFunction: _autonomousMind?.ThinkFunction,
                searchFunction: _autonomousMind?.SearchFunction,
                executeToolFunction: _autonomousMind?.ExecuteToolFunction,
                selfIndexer: _selfIndexer,
                toolRegistry: _tools);

            if (_autonomousMind != null)
            {
                _autonomousMind.Config.ThinkingIntervalSeconds = 15;
            }

            if (_config.Verbosity == OutputVerbosity.Verbose)
            {
                _agiWarmup.OnProgress += (step, percent) =>
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write($"\r  ⏳ {step} ({percent}%)".PadRight(60));
                    Console.ResetColor();
                };
            }

            var result = await _agiWarmup.WarmupAsync();

            if (_config.Verbosity == OutputVerbosity.Verbose)
            {
                Console.WriteLine(); // Clear progress line

                if (result.Success)
                    _output.WriteDebug($"AGI warmup complete in {result.Duration.TotalSeconds:F1}s");
                else
                    _output.WriteWarning($"AGI warmup limited: {result.Error ?? "Some features unavailable"}");
            }
            else if (_config.Verbosity != OutputVerbosity.Quiet)
            {
                if (result.Success)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("  ✓ Autonomous mind active");
                    Console.ResetColor();
                }
                else
                    _output.WriteWarning($"AGI warmup limited: {result.Error ?? "Some features unavailable"}");
            }

            // Warmup thought seeded into curiosity queue rather than displayed (shifts with conversation)
            if (result.Success && !string.IsNullOrEmpty(result.WarmupThought))
            {
                _autonomousMind?.InjectTopic(result.WarmupThought);
            }

            // Trigger Scrutor assembly scan now that all subsystems are registered —
            // discovers all ITool implementations and builds the IServiceProvider.
            _ = Ouroboros.Application.Tools.ServiceContainerFactory.Build();
        }
        catch (Exception ex)
        {
            _output.WriteWarning($"AGI warmup skipped: {ex.Message}");
        }
    }

    // ── SelfAssembly (delegated to SelfAssemblySubsystem) ────────────────────

    /// <summary>Analyzes capability gaps and proposes new neurons.</summary>
    public Task<IReadOnlyList<NeuronBlueprint>> AnalyzeAndProposeNeuronsAsync(CancellationToken ct = default)
        => _selfAssemblySub.AnalyzeAndProposeNeuronsAsync(ct);

    /// <summary>Attempts to assemble a neuron from a blueprint.</summary>
    public Task<Neuron?> AssembleNeuronAsync(NeuronBlueprint blueprint, CancellationToken ct = default)
        => _selfAssemblySub.AssembleNeuronAsync(blueprint, ct);


    /// <summary>
    /// Runs the main interaction loop.
    /// </summary>
    public async Task RunAsync()
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
        }

        // Handle pipe/batch/exec modes
        if (_config.PipeMode || !string.IsNullOrWhiteSpace(_config.BatchFile) || !string.IsNullOrWhiteSpace(_config.ExecCommand))
        {
            await RunNonInteractiveModeAsync();
            return;
        }

        if (_config.Verbosity == OutputVerbosity.Verbose)
            _voice.PrintHeader("OUROBOROS");

        // Greeting - let the LLM generate a natural Cortana-like greeting
        if (!_config.NoGreeting)
        {
            var greeting = await GetGreetingAsync();
            await SayWithVoiceAsync(greeting);
        }

        _isInConversationLoop = true;
        bool running = true;
        int interactionsSinceSnapshot = 0;
        while (running)
        {
            var input = await GetInputWithVoiceAsync("\n  You: ");
            if (string.IsNullOrWhiteSpace(input)) continue;

            // Track conversation
            _conversationHistory.Add($"User: {input}");
            interactionsSinceSnapshot++;

            // Feed to autonomous coordinator for topic discovery
            _autonomousCoordinator?.AddConversationContext($"User: {input}");

            // Shift autonomous mind's curiosity toward what's being discussed
            _autonomousMind?.InjectTopic(input);

            // Check for exit
            if (IsExitCommand(input))
            {
                await SayWithVoiceAsync(GetLocalizedString("Until next time! I'll keep learning while you're away."));
                running = false;
                continue;
            }

            // Process input through the agent (with pipe support)
            try
            {
                var response = await ProcessInputWithPipingAsync(input);

                // Display cost info after each response if enabled
                if (_config.ShowCosts && _costTracker != null)
                {
                    var costString = _costTracker.GetCostString();
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"  [{costString}]");
                    Console.ResetColor();
                }

                // Strip tool results for voice output (full response shown in console)
                var voiceResponse = StripToolResults(response);
                if (!string.IsNullOrWhiteSpace(voiceResponse))
                {
                    await SayWithVoiceAsync(voiceResponse);
                }

                // Also speak on side channel if enabled (non-blocking)
                Say(response);

                _conversationHistory.Add($"Ouroboros: {response}");

                // Feed response to coordinator too
                _autonomousCoordinator?.AddConversationContext($"Ouroboros: {response[..Math.Min(200, response.Length)]}");

                // Periodic personality snapshot every 10 interactions
                if (interactionsSinceSnapshot >= 10 && _personalityEngine != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _personalityEngine.SavePersonalitySnapshotAsync(_voice.ActivePersona.Name);
                            System.Diagnostics.Debug.WriteLine("[Personality] Periodic snapshot saved");
                        }
                        catch { /* Ignore */ }
                    });
                    interactionsSinceSnapshot = 0;
                }
            }
            catch (Exception ex)
            {
                await SayWithVoiceAsync($"Hmm, something went wrong: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Speaks text using the unified voice service with Rx streaming and Cortana-style voice.
    /// </summary>
    /// <param name="text">The text to speak.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="isWhisper">If true, uses soft whispering style for inner thoughts.</param>
    private async Task SayWithVoiceAsync(string text, CancellationToken ct = default, bool isWhisper = false)
    {
        // Unified VoiceModeService with Rx streaming - use WhisperAsync for inner thoughts
        if (isWhisper)
        {
            await _voice.WhisperAsync(text);
        }
        else
        {
            await _voice.SayAsync(text);
        }
    }

    /// <summary>
    /// Speaks an inner thought using soft whispering style.
    /// </summary>
    /// <param name="thought">The thought to speak.</param>
    /// <param name="ct">Cancellation token.</param>
    private Task SayThoughtWithVoiceAsync(string thought, CancellationToken ct = default)
        => SayWithVoiceAsync(thought, ct, isWhisper: true);

    /// <summary>
    /// Gets input using the unified voice service with Rx streaming.
    /// </summary>
    private async Task<string> GetInputWithVoiceAsync(string prompt, CancellationToken ct = default)
    {
        return await _voice.GetInputAsync(prompt, ct) ?? string.Empty;
    }

    // ── Pipe processing (delegated to PipeProcessingSubsystem) ─────────────────

    /// <summary>Runs in non-interactive mode for piping, batch processing, or single command execution.</summary>
    private Task RunNonInteractiveModeAsync() => _pipeSub.RunNonInteractiveModeAsync();

    /// <summary>Processes input with support for | piping syntax.</summary>
    public Task<string> ProcessInputWithPipingAsync(string input, int maxPipeDepth = 5)
        => _pipeSub.ProcessInputWithPipingAsync(input, maxPipeDepth);

    /// <summary>
    /// Processes user input and returns a response.
    /// </summary>
    public async Task<string> ProcessInputAsync(string input)
    {
        // Parse for action commands
        var action = _commandRoutingSub.ParseAction(input);

        return action.Type switch
        {
            ActionType.Help => _commandRoutingSub.GetHelpText(),
            ActionType.ListSkills => await ListSkillsAsync(),
            ActionType.ListTools => _commandRoutingSub.ListTools(),
            ActionType.LearnTopic => await LearnTopicAsync(action.Argument),
            ActionType.CreateTool => await CreateToolAsync(action.Argument),
            ActionType.UseTool => await UseToolAsync(action.Argument, action.ToolInput),
            ActionType.RunSkill => await RunSkillAsync(action.Argument),
            ActionType.Suggest => await SuggestSkillsAsync(action.Argument),
            ActionType.Plan => await PlanAsync(action.Argument),
            ActionType.Execute => await ExecuteAsync(action.Argument),
            ActionType.Status => _commandRoutingSub.GetStatus(),
            ActionType.Mood => _commandRoutingSub.GetMood(),
            ActionType.Remember => await RememberAsync(action.Argument),
            ActionType.Recall => await RecallAsync(action.Argument),
            ActionType.Query => await QueryMeTTaAsync(action.Argument),
            // Unified CLI commands
            ActionType.Ask => await AskAsync(action.Argument),
            ActionType.Pipeline => await RunPipelineAsync(action.Argument),
            ActionType.Metta => await RunMeTTaExpressionAsync(action.Argument),
            ActionType.Orchestrate => await OrchestrateAsync(action.Argument),
            ActionType.Network => await NetworkCommandAsync(action.Argument),
            ActionType.Dag => await DagCommandAsync(action.Argument),
            ActionType.Affect => await AffectCommandAsync(action.Argument),
            ActionType.Environment => await EnvironmentCommandAsync(action.Argument),
            ActionType.Maintenance => await MaintenanceCommandAsync(action.Argument),
            ActionType.Policy => await PolicyCommandAsync(action.Argument),
            ActionType.Explain => _commandRoutingSub.ExplainDsl(action.Argument),
            ActionType.Test => await RunTestAsync(action.Argument),
            // Merged from ImmersiveMode and Skills mode
            ActionType.Consciousness => GetConsciousnessState(),
            ActionType.Tokens => _commandRoutingSub.GetDslTokens(),
            ActionType.Fetch => await FetchResearchAsync(action.Argument),
            ActionType.Process => await ProcessLargeInputAsync(action.Argument),
            // Self-execution and sub-agent commands
            ActionType.SelfExec => await SelfExecCommandAsync(action.Argument),
            ActionType.SubAgent => await SubAgentCommandAsync(action.Argument),
            ActionType.Epic => await EpicCommandAsync(action.Argument),
            ActionType.Goal => await GoalCommandAsync(action.Argument),
            ActionType.Delegate => await DelegateCommandAsync(action.Argument),
            ActionType.SelfModel => await SelfModelCommandAsync(action.Argument),
            ActionType.Evaluate => await EvaluateCommandAsync(action.Argument),
            // Emergent behavior commands
            ActionType.Emergence => await EmergenceCommandAsync(action.Argument),
            ActionType.Dream => await DreamCommandAsync(action.Argument),
            ActionType.Introspect => await IntrospectCommandAsync(action.Argument),
            // Push mode commands
            ActionType.Approve => await ApproveIntentionAsync(action.Argument),
            ActionType.Reject => await RejectIntentionAsync(action.Argument),
            ActionType.Pending => ListPendingIntentions(),
            ActionType.PushPause => PausePushMode(),
            ActionType.PushResume => ResumePushMode(),
            ActionType.CoordinatorCommand => _commandRoutingSub.ProcessCoordinatorCommand(input),
            // Self-modification commands (direct tool invocation)
            ActionType.SaveCode => await SaveCodeCommandAsync(action.Argument),
            ActionType.SaveThought => await SaveThoughtCommandAsync(action.Argument),
            ActionType.ReadMyCode => await ReadMyCodeCommandAsync(action.Argument),
            ActionType.SearchMyCode => await SearchMyCodeCommandAsync(action.Argument),
            ActionType.AnalyzeCode => await AnalyzeCodeCommandAsync(action.Argument),
            // Index commands
            ActionType.Reindex => await ReindexFullAsync(),
            ActionType.ReindexIncremental => await ReindexIncrementalAsync(),
            ActionType.IndexSearch => await IndexSearchAsync(action.Argument),
            ActionType.IndexStats => await GetIndexStatsAsync(),
            // AGI subsystem commands
            ActionType.AgiStatus => GetAgiStatus(),
            ActionType.AgiCouncil => await RunCouncilDebateAsync(action.Argument),
            ActionType.AgiIntrospect => GetIntrospectionReport(),
            ActionType.AgiWorld => GetWorldModelStatus(),
            ActionType.AgiCoordinate => await RunAgentCoordinationAsync(action.Argument),
            ActionType.AgiExperience => GetExperienceBufferStatus(),
            ActionType.PromptOptimize => GetPromptOptimizerStatus(),
            ActionType.Chat => await ChatAsync(input),
            _ => await ChatAsync(input)
        };
    }


    private static readonly string[] GreetingStyles =
    [
        "playfully teasing about the time since last session",
        "genuinely curious about what project they're working on",
        "warmly welcoming like an old friend",
        "subtly competitive, eager to tackle a challenge together",
        "contemplative and philosophical",
        "energetically enthusiastic about the day ahead",
        "calm and focused, ready for serious work",
        "slightly mysterious, hinting at discoveries to share"
    ];

    private static readonly string[] GreetingMoods =
    [
        "witty and sharp",
        "warm and inviting",
        "playfully sarcastic",
        "thoughtfully curious",
        "quietly confident",
        "gently encouraging"
    ];

    private async Task<string> GetGreetingAsync()
    {
        var persona = _voice.ActivePersona;
        var hour = DateTime.Now.Hour;
        var timeOfDay = GetLocalizedTimeOfDay(hour);

        var style = GreetingStyles[Random.Shared.Next(GreetingStyles.Length)];
        var mood = GreetingMoods[Random.Shared.Next(GreetingMoods.Length)];
        var dayOfWeek = DateTime.Now.DayOfWeek;
        var uniqueSeed = Guid.NewGuid().GetHashCode() % 10000; // True unique variation

        // Add language directive if culture is set
        var languageDirective = GetLanguageDirective();

        var prompt = PromptResources.PersonaGreeting(
            languageDirective, persona.Name, timeOfDay,
            dayOfWeek.ToString(), style, mood, uniqueSeed.ToString());

        try
        {
            if (_llm?.InnerModel == null)
                return GetRandomFallbackGreeting(hour);

            var response = await _llm.InnerModel.GenerateTextAsync(prompt);
            return response.Trim().Trim('"');
        }
        catch
        {
            return GetRandomFallbackGreeting(hour);
        }
    }

    private string GetRandomFallbackGreeting(int hour)
    {
        var timeOfDay = GetLocalizedTimeOfDay(hour);
        var fallbacks = GetLocalizedFallbackGreetings(timeOfDay);
        return fallbacks[Random.Shared.Next(fallbacks.Length)];
    }

    private string GetLocalizedTimeOfDay(int hour) => _localizationSub.GetLocalizedTimeOfDay(hour);
    private string[] GetLocalizedFallbackGreetings(string timeOfDay) => _localizationSub.GetLocalizedFallbackGreetings(timeOfDay);
    private string GetLocalizedString(string key) => _localizationSub.GetLocalizedString(key);
    private string GetLanguageDirective() => _localizationSub.GetLanguageDirective();


    private async Task<string> ListSkillsAsync()
    {
        if (_skills == null) return "I don't have a skill registry set up yet.";

        var skills = await _skills.FindMatchingSkillsAsync("", null);
        if (!skills.Any())
            return "I haven't learned any skills yet. Try 'learn about' something!";

        var list = string.Join(", ", skills.Take(10).Select(s => s.Name));
        return $"I know {skills.Count} skills: {list}" + (skills.Count > 10 ? "..." : "");
    }


    private async Task<string> LearnTopicAsync(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return "What would you like me to learn about?";

        var sb = new StringBuilder();
        sb.AppendLine($"Learning about: {topic}");

        // Step 1: Research the topic via LLM
        string? research = null;
        if (_llm != null)
        {
            try
            {
                var (response, toolCalls) = await _llm.GenerateWithToolsAsync(
                    $"Research and explain key concepts about: {topic}. Include practical applications and how this knowledge could be used.");
                research = response;
                sb.AppendLine($"\n📚 Research Summary:\n{response[..Math.Min(500, response.Length)]}...");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"⚠ Research phase had issues: {ex.Message}");
            }
        }

        // Step 2: Try to create a tool capability
        if (_toolLearner != null)
        {
            try
            {
                var toolResult = await _toolLearner.FindOrCreateToolAsync(topic, _tools);
                toolResult.Match(
                    success =>
                    {
                        sb.AppendLine($"\n🔧 {(success.WasCreated ? "Created new" : "Found existing")} tool: '{success.Tool.Name}'");
                        AddToolAndRefreshLlm(success.Tool);
                    },
                    error => sb.AppendLine($"⚠ Tool creation: {error}"));
            }
            catch (Exception ex)
            {
                sb.AppendLine($"⚠ Tool learner: {ex.Message}");
            }
        }

        // Step 3: Register as a skill if we have skill registry
        if (_skills != null && !string.IsNullOrWhiteSpace(research))
        {
            try
            {
                var skillName = SanitizeSkillName(topic);
                var existingSkill = _skills.GetSkill(skillName);

                if (existingSkill == null)
                {
                    var skill = new Skill(
                        Name: skillName,
                        Description: $"Knowledge about {topic}: {research[..Math.Min(200, research.Length)]}",
                        Prerequisites: new List<string>(),
                        Steps: new List<PlanStep>
                        {
                            new PlanStep(
                                $"Apply knowledge about {topic}",
                                new Dictionary<string, object> { ["topic"] = topic, ["research"] = research },
                                $"Use {topic} knowledge effectively",
                                0.7)
                        },
                        SuccessRate: 0.8,
                        UsageCount: 0,
                        CreatedAt: DateTime.UtcNow,
                        LastUsed: DateTime.UtcNow);

                    await _skills.RegisterSkillAsync(skill.ToAgentSkill());
                    sb.AppendLine($"\n✓ Registered skill: '{skillName}'");
                }
                else
                {
                    _skills.RecordSkillExecution(skillName, true, 0L);
                    sb.AppendLine($"\n↺ Updated existing skill: '{skillName}'");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"⚠ Skill registration: {ex.Message}");
            }
        }

        // Step 4: Add to MeTTa knowledge base
        if (_mettaEngine != null)
        {
            try
            {
                var atomName = SanitizeSkillName(topic);
                await _mettaEngine.AddFactAsync($"(: {atomName} Concept)");
                await _mettaEngine.AddFactAsync($"(learned {atomName} \"{DateTime.UtcNow:O}\")");

                if (!string.IsNullOrWhiteSpace(research))
                {
                    var summary = research.Length > 100 ? research[..100].Replace("\"", "'") : research.Replace("\"", "'");
                    await _mettaEngine.AddFactAsync($"(summary {atomName} \"{summary}\")");
                }

                sb.AppendLine($"\n🧠 Added to MeTTa knowledge base: {atomName}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"⚠ MeTTa: {ex.Message}");
            }
        }

        // Step 5: Track in global workspace
        _globalWorkspace?.AddItem(
            $"Learned: {topic}\n{research?[..Math.Min(200, research?.Length ?? 0)]}",
            WorkspacePriority.Normal,
            "learning",
            new List<string> { "learned", topic.ToLowerInvariant().Replace(" ", "-") });

        // Step 6: Update capability if available
        if (_capabilityRegistry != null)
        {
            var result = AutonomySubsystem.CreateCapabilityPlanExecutionResult(true, TimeSpan.FromSeconds(2), $"learn:{topic}");
            await _capabilityRegistry.UpdateCapabilityAsync("natural_language", result);
        }

        return sb.ToString();
    }

    private static string SanitizeSkillName(string name)
    {
        return name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("'", "")
            .Replace("\"", "")
            .Replace("(", "")
            .Replace(")", "");
    }

    private async Task<string> CreateToolAsync(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return "What kind of tool should I create?";

        if (_toolFactory == null)
            return "I need an LLM connection to create new tools.";

        try
        {
            var result = await _toolFactory.CreateToolAsync(toolName, $"A tool for {toolName}");
            return result.Match(
                tool =>
                {
                    AddToolAndRefreshLlm(tool);
                    return $"Done! I created a '{toolName}' tool. You can now use it.";
                },
                error => $"I couldn't create that tool: {error}");
        }
        catch (Exception ex)
        {
            return $"I couldn't create that tool: {ex.Message}";
        }
    }

    private async Task<string> UseToolAsync(string toolName, string? input)
    {
        var tool = _tools.Get(toolName) ?? _tools.All.FirstOrDefault(t =>
            t.Name.Contains(toolName, StringComparison.OrdinalIgnoreCase));

        if (tool == null)
            return $"I don't have a '{toolName}' tool. Try 'list tools' to see what's available.";

        try
        {
            var result = await tool.InvokeAsync(input ?? "");
            return $"Result: {result}";
        }
        catch (Exception ex)
        {
            return $"The tool ran into an issue: {ex.Message}";
        }
    }

    private async Task<string> RunSkillAsync(string skillName)
    {
        if (_skills == null) return "Skills not available.";

        var skill = _skills.GetSkill(skillName);
        if (skill == null)
        {
            var matches = await _skills.FindMatchingSkillsAsync(skillName);
            if (matches.Any())
            {
                skill = matches.First().ToAgentSkill();
            }
            else
            {
                return $"I don't know a skill called '{skillName}'. Try 'list skills'.";
            }
        }

        // Execute skill steps
        var results = new List<string>();
        foreach (var step in skill.ToSkill().Steps)
        {
            results.Add($"• {step.Action}: {step.ExpectedOutcome}");
        }

        _skills.RecordSkillExecution(skill.Name, true, 0L);
        return $"Running '{skill.Name}':\n" + string.Join("\n", results);
    }

    private async Task<string> SuggestSkillsAsync(string goal)
    {
        if (_skills == null) return "Skills not available.";

        var matches = await _skills.FindMatchingSkillsAsync(goal);
        if (!matches.Any())
            return $"I don't have skills matching '{goal}' yet. Try learning about it first!";

        var suggestions = string.Join(", ", matches.Take(5).Select(s => s.Name));
        return $"For '{goal}', I'd suggest: {suggestions}";
    }

    private async Task<string> PlanAsync(string goal)
    {
        if (_orchestrator == null)
        {
            // Fallback to LLM-based planning
            if (_llm != null)
            {
                var (plan, _) = await _llm.GenerateWithToolsAsync(
                    $"Create a step-by-step plan for: {goal}. Format as numbered steps.");
                return plan;
            }
            return "I need an orchestrator or LLM to create plans.";
        }

        var planResult = await _orchestrator.PlanAsync(goal);
        return planResult.Match(
            plan =>
            {
                var steps = string.Join("\n", plan.Steps.Select((s, i) => $"  {i + 1}. {s.Action}"));
                return $"Here's my plan for '{goal}':\n{steps}";
            },
            error => $"I couldn't plan that: {error}");
    }

    private async Task<string> ExecuteAsync(string goal)
    {
        if (_orchestrator == null)
            return await ChatAsync($"Help me accomplish: {goal}");

        var planResult = await _orchestrator.PlanAsync(goal);
        return await planResult.Match(
            async plan =>
            {
                var execResult = await _orchestrator.ExecuteAsync(plan);
                return execResult.Match(
                    result => result.Success
                        ? $"Done! {result.FinalOutput ?? "Goal accomplished."}"
                        : $"Partially completed: {result.FinalOutput}",
                    error => $"Execution failed: {error}");
            },
            error => Task.FromResult($"Couldn't plan: {error}"));
    }



    private string GetConsciousnessState()
        => ((CognitiveSubsystem)_cognitiveSub).GetConsciousnessState();


    /// <summary>
    /// Fetches research from arXiv and creates a new skill.
    /// </summary>
    private async Task<string> FetchResearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "Usage: fetch <research query>";
        }

        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            string url = $"http://export.arxiv.org/api/query?search_query=all:{Uri.EscapeDataString(query)}&start=0&max_results=5";
            string xml = await httpClient.GetStringAsync(url);
            var doc = System.Xml.Linq.XDocument.Parse(xml);
            System.Xml.Linq.XNamespace atom = "http://www.w3.org/2005/Atom";
            var entries = doc.Descendants(atom + "entry").Take(5).ToList();

            if (entries.Count == 0)
            {
                return $"No research found for '{query}'. Try a different search term.";
            }

            // Create skill name from query
            string skillName = string.Join("", query.Split(' ')
                .Select(w => w.Length > 0 ? char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w[1..].ToLowerInvariant() : "") : "")) + "Analysis";

            // Register new skill if we have a skill registry
            if (_skills != null)
            {
                var newSkill = new Skill(
                    skillName,
                    $"Analysis methodology from '{query}' research",
                    new List<string> { "research-context" },
                    new List<PlanStep>
                    {
                        new("Gather sources", new Dictionary<string, object> { ["query"] = query }, "Relevant papers", 0.9),
                        new("Extract patterns", new Dictionary<string, object> { ["method"] = "identify" }, "Key techniques", 0.85),
                        new("Synthesize", new Dictionary<string, object> { ["action"] = "combine" }, "Actionable knowledge", 0.8)
                    },
                    0.75, 0, DateTime.UtcNow, DateTime.UtcNow);
                _skills.RegisterSkill(newSkill.ToAgentSkill());
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Found {entries.Count} papers on '{query}':");
            sb.AppendLine();

            foreach (var entry in entries)
            {
                var title = entry.Element(atom + "title")?.Value?.Trim().Replace("\n", " ");
                var summary = entry.Element(atom + "summary")?.Value?.Trim();
                var truncatedSummary = summary?.Length > 150 ? summary[..150] + "..." : summary;

                sb.AppendLine($"  • {title}");
                sb.AppendLine($"    {truncatedSummary}");
                sb.AppendLine();
            }

            if (_skills != null)
            {
                sb.AppendLine($"✓ New skill created: UseSkill_{skillName}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error fetching research: {ex.Message}";
        }
    }

    /// <summary>
    /// Processes large input using divide-and-conquer orchestration.
    /// </summary>
    private async Task<string> ProcessLargeInputAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "Usage: process <large text or file path>";
        }

        // Check if input is a file path
        string textToProcess = input;
        if (File.Exists(input))
        {
            try
            {
                textToProcess = await File.ReadAllTextAsync(input);
            }
            catch (Exception ex)
            {
                return $"Error reading file: {ex.Message}";
            }
        }

        if (_divideAndConquer == null)
        {
            // Fall back to regular processing
            if (_chatModel == null)
            {
                return "No LLM available for processing.";
            }
            return await _chatModel.GenerateTextAsync($"Summarize and extract key points:\n\n{textToProcess}");
        }

        try
        {
            var chunks = _divideAndConquer.DivideIntoChunks(textToProcess);
            var result = await _divideAndConquer.ExecuteAsync(
                "Summarize and extract key points:",
                chunks);

            return result.Match(
                success => $"Processed {chunks.Count} chunks:\n\n{success}",
                error => $"Processing error: {error}");
        }
        catch (Exception ex)
        {
            return $"Divide-and-conquer processing failed: {ex.Message}";
        }
    }

    private async Task<string> RememberAsync(string info)
    {
        if (_personalityEngine != null && _personalityEngine.HasMemory)
        {
            await _personalityEngine.StoreConversationMemoryAsync(
                _voice.ActivePersona.Name,
                $"Remember: {info}",
                "Memory stored.",
                "user_memory",
                "neutral",
                0.8);
            return "Got it, I'll remember that.";
        }
        return "I don't have memory storage set up, but I'll try to keep it in mind for this session.";
    }

    private async Task<string> RecallAsync(string topic)
    {
        if (_personalityEngine != null && _personalityEngine.HasMemory)
        {
            var memories = await _personalityEngine.RecallConversationsAsync(topic, _voice.ActivePersona.Name, 5);
            if (memories.Any())
            {
                var recollections = memories.Take(3).Select(m => m.UserMessage);
                return "I remember: " + string.Join("; ", recollections);
            }
        }
        return $"I don't have specific memories about '{topic}' yet.";
    }

    private async Task<string> QueryMeTTaAsync(string query)
    {
        var result = await QueryMeTTaResultAsync(query);
        return result.Match(
            success => $"MeTTa result: {success}",
            error => $"Query error: {error}");
    }

    private async Task<Result<string, string>> QueryMeTTaResultAsync(string query)
    {
        if (_mettaEngine == null)
            return Result<string, string>.Failure("MeTTa symbolic reasoning isn't available.");

        return await _mettaEngine.ExecuteQueryAsync(query, CancellationToken.None);
    }

    // ================================================================
    // UNIFIED CLI COMMANDS - All Ouroboros capabilities in one place
    // ================================================================

    /// <summary>
    /// Ask a single question (routes to AskCommands CLI handler).
    /// </summary>
    private async Task<string> AskAsync(string question)
    {
        var result = await AskResultAsync(question);
        return result.Match(success => success, error => $"Error asking question: {error}");
    }

    private async Task<Result<string, string>> AskResultAsync(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return Result<string, string>.Failure("What would you like to ask?");

        var askOpts = new AskOptions
        {
            Question = question,
            Model = "llama3",
            Temperature = 0.7,
            MaxTokens = 2048,
            TimeoutSeconds = 120,
            Stream = false,
            Culture = Thread.CurrentThread.CurrentCulture.Name,
            Voice = false,
            Agent = true,
            Rag = false,
            Router = "none",
            Debug = false,
            StrictModel = false
        };

        return await CaptureConsoleOutAsync(() => AskCommands.RunAskAsync(askOpts));
    }

    // IAgentFacade explicit implementations for monadic operations
    Task<Result<string, string>> IAgentFacade.AskResultAsync(string question) => AskResultAsync(question);
    Task<Result<string, string>> IAgentFacade.RunPipelineResultAsync(string dsl) => RunPipelineResultAsync(dsl);
    Task<Result<string, string>> IAgentFacade.RunMeTTaExpressionResultAsync(string expression) => RunMeTTaExpressionResultAsync(expression);
    Task<Result<string, string>> IAgentFacade.QueryMeTTaResultAsync(string query) => QueryMeTTaResultAsync(query);

    /// <summary>
    /// Run a DSL pipeline expression (routes to PipelineCommands CLI handler).
    /// </summary>
    private async Task<string> RunPipelineAsync(string dsl)
    {
        var result = await RunPipelineResultAsync(dsl);
        return result.Match(success => success, error => $"Pipeline error: {error}");
    }

    private async Task<Result<string, string>> RunPipelineResultAsync(string dsl)
    {
        if (string.IsNullOrWhiteSpace(dsl))
            return Result<string, string>.Failure("Please provide a DSL expression. Example: 'pipeline draft → critique → final'");

        var pipelineOpts = new PipelineOptions
        {
            Dsl = dsl,
            Model = "llama3",
            Temperature = 0.7,
            MaxTokens = 4096,
            TimeoutSeconds = 120,
            Voice = false,
            Culture = Thread.CurrentThread.CurrentCulture.Name,
            Debug = false
        };

        return await CaptureConsoleOutAsync(() => PipelineCommands.RunPipelineAsync(pipelineOpts));
    }

    /// <summary>
    /// Execute a MeTTa expression directly (routes to MeTTaCommands CLI handler).
    /// </summary>
    private async Task<string> RunMeTTaExpressionAsync(string expression)
    {
        var result = await RunMeTTaExpressionResultAsync(expression);
        return result.Match(success => success, error => $"MeTTa execution failed: {error}");
    }

    private async Task<Result<string, string>> RunMeTTaExpressionResultAsync(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return Result<string, string>.Failure("Please provide a MeTTa expression. Example: '!(+ 1 2)' or '(= (greet $x) (Hello $x))'");

        var mettaOpts = new MeTTaOptions
        {
            Goal = expression,
            Voice = false,
            Culture = Thread.CurrentThread.CurrentCulture.Name,
            Debug = false
        };

        return await CaptureConsoleOutAsync(() => MeTTaCommands.RunMeTTaAsync(mettaOpts));
    }

    // Helper to capture CLI command output and return as Result
    private static async Task<Result<string, string>> CaptureConsoleOutAsync(Func<Task> action)
    {
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            try
            {
                await action();
                return Result<string, string>.Success(writer.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MONADIC STEP CONSTRUCTS (for functional composition)
    // ═══════════════════════════════════════════════════════════════════════════
    // These expose core actions as Step<string, Result<string,string>> so they can
    // be composed using Pipeline/Step combinators across the system.

    /// <summary>
    /// Functional step to ask a question. Input: question string. Output: Result text.
    /// </summary>
    public Step<string, Result<string, string>> AskStep()
        => async question => await AskResultAsync(question);

    /// <summary>
    /// Functional step to run a Pipeline DSL expression. Input: DSL string. Output: Result text.
    /// </summary>
    public Step<string, Result<string, string>> PipelineStep()
        => async dsl => await RunPipelineResultAsync(dsl);

    /// <summary>
    /// Functional step to execute a MeTTa expression directly. Input: expression. Output: Result text.
    /// </summary>
    public Step<string, Result<string, string>> MeTTaExpressionStep()
        => async expression => await RunMeTTaExpressionResultAsync(expression);

    /// <summary>
    /// Functional step to query the MeTTa engine. Input: query string. Output: Result text.
    /// </summary>
    public Step<string, Result<string, string>> MeTTaQueryStep()
        => async query => await QueryMeTTaResultAsync(query);

    /// <summary>
    /// Functional step to orchestrate a multi-step goal. Input: goal. Output: Result text.
    /// </summary>
    public Step<string, Result<string, string>> OrchestrateStep()
        => async goal =>
        {
            try
            {
                var text = await OrchestrateAsync(goal);
                return Result<string, string>.Success(text);
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        };

    /// <summary>
    /// Functional step to produce a plan for a goal. Input: goal. Output: Result text.
    /// </summary>
    public Step<string, Result<string, string>> PlanStep()
        => async goal =>
        {
            try
            {
                var text = await PlanAsync(goal);
                return Result<string, string>.Success(text);
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        };

    /// <summary>
    /// Functional step to execute a goal with planning. Input: goal. Output: Result text.
    /// </summary>
    public Step<string, Result<string, string>> ExecuteStep()
        => async goal =>
        {
            try
            {
                var text = await ExecuteAsync(goal);
                return Result<string, string>.Success(text);
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        };

    /// <summary>
    /// Functional step to fetch research (arXiv). Input: query. Output: Result text.
    /// </summary>
    public Step<string, Result<string, string>> FetchResearchStep()
        => async query =>
        {
            try
            {
                var text = await FetchResearchAsync(query);
                return Result<string, string>.Success(text);
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        };

    /// <summary>
    /// Functional step to process large input via divide-and-conquer. Input: text-or-filepath. Output: Result text.
    /// </summary>
    public Step<string, Result<string, string>> ProcessLargeInputStep()
        => async input =>
        {
            try
            {
                var text = await ProcessLargeInputAsync(input);
                return Result<string, string>.Success(text);
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        };

    /// <summary>
    /// Functional step to remember information. Input: info. Output: Result text.
    /// </summary>
    public Step<string, Result<string, string>> RememberStep()
        => async info =>
        {
            try
            {
                var text = await RememberAsync(info);
                return Result<string, string>.Success(text);
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        };

    /// <summary>
    /// Functional step to recall information. Input: topic. Output: Result text.
    /// </summary>
    public Step<string, Result<string, string>> RecallStep()
        => async topic =>
        {
            try
            {
                var text = await RecallAsync(topic);
                return Result<string, string>.Success(text);
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        };

    /// <summary>
    /// Functional step to run a named skill. Input: skill name. Output: Result text.
    /// </summary>
    public Step<string, Result<string, string>> RunSkillStep()
        => async skillName =>
        {
            try
            {
                var text = await RunSkillAsync(skillName);
                return Result<string, string>.Success(text);
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        };

    /// <summary>
    /// Functional step factory to invoke a specific tool. The returned step takes the tool input string.
    /// </summary>
    public Step<string, Result<string, string>> UseToolStep(string toolName)
        => async toolInput =>
        {
            try
            {
                var tool = _tools.Get(toolName) ?? _tools.All.FirstOrDefault(t => t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));
                if (tool is null)
                    return Result<string, string>.Failure($"Tool not found: {toolName}");

                Result<string, string> result = await tool.InvokeAsync(toolInput ?? string.Empty);
                return result;
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        };

    /// <summary>
    /// Orchestrate a complex multi-step task (routes to OrchestratorCommands CLI handler).
    /// </summary>
    private async Task<string> OrchestrateAsync(string goal)
    {
        if (string.IsNullOrWhiteSpace(goal))
            return "What would you like me to orchestrate?";

        try
        {
            var orchestratorOpts = new OrchestratorOptions
            {
                Goal = goal,
                Model = "llama3",
                Temperature = 0.7,
                MaxTokens = 4096,
                TimeoutSeconds = 300,
                Voice = false,
                Debug = false,
                Culture = Thread.CurrentThread.CurrentCulture.Name
            };

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await OrchestratorCommands.RunOrchestratorAsync(orchestratorOpts);
                    return writer.ToString();
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        catch (Exception ex)
        {
            return $"Orchestration error: {ex.Message}";
        }
    }

    /// <summary>
    /// Network status and management (routes to NetworkCommands CLI handler).
    /// </summary>
    private async Task<string> NetworkCommandAsync(string subCommand)
    {
        try
        {
            var networkOpts = new NetworkOptions();

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await NetworkCommands.RunAsync(networkOpts);
                    return writer.ToString();
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        catch (Exception ex)
        {
            return $"Network command error: {ex.Message}";
        }
    }

    /// <summary>
    /// DAG visualization and management (routes to DagCommands CLI handler).
    /// </summary>
    private async Task<string> DagCommandAsync(string subCommand)
    {
        try
        {
            var dagOpts = new DagOptions
            {
                Command = subCommand?.ToLowerInvariant().Trim() ?? "show"
            };

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await DagCommands.RunDagAsync(dagOpts);
                    return writer.ToString();
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        catch (Exception ex)
        {
            return $"DAG command error: {ex.Message}";
        }
    }

    /// <summary>
    /// Affect and emotional state (routes to AffectCommands CLI handler).
    /// </summary>
    private async Task<string> AffectCommandAsync(string subCommand)
    {
        try
        {
            var affectOpts = new AffectOptions
            {
                Command = subCommand?.ToLowerInvariant().Trim() ?? "status"
            };

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await AffectCommands.RunAffectAsync(affectOpts);
                    return writer.ToString();
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        catch (Exception ex)
        {
            return $"Affect command error: {ex.Message}";
        }
    }

    /// <summary>
    /// Environment detection and configuration (routes to EnvironmentCommands CLI handler).
    /// </summary>
    private async Task<string> EnvironmentCommandAsync(string subCommand)
    {
        try
        {
            var envOpts = new EnvironmentOptions
            {
                Command = subCommand?.ToLowerInvariant().Trim() ?? "status"
            };

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await EnvironmentCommands.RunEnvironmentCommandAsync(envOpts);
                    return writer.ToString();
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        catch (Exception ex)
        {
            return $"Environment command error: {ex.Message}";
        }
    }

    /// <summary>
    /// Maintenance operations (routes to MaintenanceCommands CLI handler).
    /// </summary>
    private async Task<string> MaintenanceCommandAsync(string subCommand)
    {
        try
        {
            var maintenanceOpts = new MaintenanceOptions
            {
                Command = subCommand?.ToLowerInvariant().Trim() ?? "status"
            };

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await MaintenanceCommands.RunMaintenanceAsync(maintenanceOpts);
                    return writer.ToString();
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        catch (Exception ex)
        {
            return $"Maintenance command error: {ex.Message}";
        }
    }

    /// <summary>
    /// Policy management - routes to the real CLI PolicyCommands.
    /// </summary>
    private async Task<string> PolicyCommandAsync(string subCommand)
    {
        var cmd = subCommand.ToLowerInvariant().Trim();

        // Parse policy subcommand and create appropriate PolicyOptions
        var args = subCommand.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        string command = args.Length > 0 ? args[0] : "list";
        string argument = args.Length > 1 ? args[1] : "";

        try
        {
            // Create PolicyOptions from parsed command
            var policyOpts = new PolicyOptions
            {
                Command = command,
                Culture = _config.Culture,
                Format = "summary",
                Limit = 50,
                Verbose = _config.Debug
            };

            // Parse arguments based on command type
            if (command == "list")
            {
                policyOpts.Format = argument switch
                {
                    "json" => "json",
                    "table" => "table",
                    _ => "summary"
                };
            }
            else if (command == "show")
            {
                policyOpts.Command = "list";
            }
            else if (command == "enforce")
            {
                policyOpts.Command = "enforce";
                // Parse arguments: --enable-self-mod --risk-level Low
                if (argument.Contains("--enable-self-mod"))
                {
                    policyOpts.EnableSelfModification = true;
                }
                if (argument.Contains("--risk-level"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(argument, @"--risk-level\s+(\w+)");
                    if (match.Success)
                    {
                        policyOpts.RiskLevel = match.Groups[1].Value;
                    }
                }
            }
            else if (command == "audit")
            {
                policyOpts.Command = "audit";
                if (int.TryParse(argument, out var limit))
                {
                    policyOpts.Limit = limit;
                }
            }
            else if (command == "simulate")
            {
                policyOpts.Command = "simulate";
                if (System.Guid.TryParse(argument, out _))
                {
                    policyOpts.PolicyId = argument;
                }
            }
            else if (command == "create")
            {
                policyOpts.Command = "create";
                var parts = argument.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    policyOpts.Name = parts[0].Trim();
                }
                if (parts.Length > 1)
                {
                    policyOpts.Description = parts[1].Trim();
                }
            }
            else if (command == "approve")
            {
                policyOpts.Command = "approve";
                var parts = argument.Split(' ', 2);
                if (parts.Length > 0 && System.Guid.TryParse(parts[0], out _))
                {
                    policyOpts.ApprovalId = parts[0];
                }
                if (parts.Length > 1)
                {
                    policyOpts.Decision = "approve";
                    policyOpts.ApproverId = "agent";
                }
            }

            // Call the real PolicyCommands
            await PolicyCommands.RunPolicyAsync(policyOpts);
            return $"Policy command executed: {command}";
        }
        catch (Exception ex)
        {
            return $"Policy command failed: {ex.Message}";
        }
    }


    /// <summary>
    /// Run tests (unified test command).
    /// </summary>
    private async Task<string> RunTestAsync(string testSpec)
    {
        if (string.IsNullOrWhiteSpace(testSpec))
        {
            return @"Test Commands:
• 'test llm' - Test LLM connectivity
• 'test metta' - Test MeTTa engine
• 'test embedding' - Test embedding model
• 'test all' - Run all connectivity tests";
        }

        var cmd = testSpec.ToLowerInvariant().Trim();

        if (cmd == "llm")
        {
            if (_chatModel == null) return "✗ LLM: Not configured";
            try
            {
                var response = await _chatModel.GenerateTextAsync("Say OK");
                return $"✓ LLM: {_config.Model} responds correctly";
            }
            catch (Exception ex)
            {
                return $"✗ LLM: {ex.Message}";
            }
        }

        if (cmd == "metta")
        {
            if (_mettaEngine == null) return "✗ MeTTa: Not configured";
            var result = await _mettaEngine.ExecuteQueryAsync("!(+ 1 2)", CancellationToken.None);
            return result.Match(
                output => $"✓ MeTTa: Engine working (1+2={output})",
                error => $"✗ MeTTa: {error}");
        }

        if (cmd == "embedding")
        {
            if (_embedding == null) return "✗ Embedding: Not configured";
            try
            {
                var vec = await _embedding.CreateEmbeddingsAsync("test");
                return $"✓ Embedding: {_config.EmbedModel} (dim={vec.Length})";
            }
            catch (Exception ex)
            {
                return $"✗ Embedding: {ex.Message}";
            }
        }

        if (cmd == "all")
        {
            var results = new List<string>
            {
                await RunTestAsync("llm"),
                await RunTestAsync("metta"),
                await RunTestAsync("embedding")
            };
            return "Test Results:\n" + string.Join("\n", results);
        }

        return $"Unknown test: {testSpec}. Try 'test llm', 'test metta', 'test embedding', or 'test all'.";
    }

    /// <summary>Runs the full LLM chat pipeline (delegated to ChatSubsystem).</summary>
    private Task<string> ChatAsync(string input) => _chatSub.ChatAsync(input);




    private static bool IsExitCommand(string input)
    {
        var exitWords = new[] { "exit", "quit", "goodbye", "bye", "later", "see you", "q!", "stop" };
        return exitWords.Any(w => input.Equals(w, StringComparison.OrdinalIgnoreCase) ||
                                  input.StartsWith(w + " ", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if a Tapo device type is a camera (for RTSP streaming).
    /// </summary>
    private static bool IsCameraDeviceType(Ouroboros.Providers.Tapo.TapoDeviceType deviceType) =>
        deviceType is Ouroboros.Providers.Tapo.TapoDeviceType.C100
            or Ouroboros.Providers.Tapo.TapoDeviceType.C200
            or Ouroboros.Providers.Tapo.TapoDeviceType.C210
            or Ouroboros.Providers.Tapo.TapoDeviceType.C220
            or Ouroboros.Providers.Tapo.TapoDeviceType.C310
            or Ouroboros.Providers.Tapo.TapoDeviceType.C320
            or Ouroboros.Providers.Tapo.TapoDeviceType.C420
            or Ouroboros.Providers.Tapo.TapoDeviceType.C500
            or Ouroboros.Providers.Tapo.TapoDeviceType.C520;

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

    // ActionType enum moved to Commands/ActionType.cs (internal)

    /// <summary>
    /// Processes an initial goal provided via command line.
    /// </summary>
    public async Task ProcessGoalAsync(string goal)
    {
        var response = await ExecuteAsync(goal);
        await SayWithVoiceAsync(response);
        Say(response);  // Side channel
        _conversationHistory.Add($"Goal: {goal}");
        _conversationHistory.Add($"Ouroboros: {response}");
    }

    /// <summary>
    /// Processes an initial question provided via command line.
    /// </summary>
    public async Task ProcessQuestionAsync(string question)
    {
        var response = await ChatAsync(question);
        await SayWithVoiceAsync(response);
        Say(response);  // Side channel
        _conversationHistory.Add($"User: {question}");
        _conversationHistory.Add($"Ouroboros: {response}");
    }

    /// <summary>
    /// Processes and executes a pipeline DSL string.
    /// </summary>
    public async Task ProcessDslAsync(string dsl)
    {
        try
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n  📜 Executing DSL: {dsl}\n");
            Console.ResetColor();

            // Explain the DSL first
            var explanation = PipelineDsl.Explain(dsl);
            Console.WriteLine(explanation);

            // Build and execute the pipeline
            if (_embedding != null && _llm != null)
            {
                var store = new TrackedVectorStore();
                var dataSource = DataSource.FromPath(".");
                var branch = new PipelineBranch("ouroboros-dsl", store, dataSource);

                var state = new CliPipelineState
                {
                    Branch = branch,
                    Llm = _llm,
                    Tools = _tools,
                    Embed = _embedding,
                    Trace = _config.Debug,
                    NetworkTracker = _networkTracker  // Enable automatic step reification
                };

                // Initial tracking of the branch
                _networkTracker?.TrackBranch(branch);

                // Track capability usage for self-improvement
                var startTime = DateTime.UtcNow;
                var success = true;

                try
                {
                    var step = PipelineDsl.Build(dsl);
                    state = await step(state);
                }
                catch (Exception stepEx)
                {
                    success = false;
                    throw new InvalidOperationException($"Pipeline step failed: {stepEx.Message}", stepEx);
                }

                // Final update to capture all step events
                if (_networkTracker != null)
                {
                    var trackResult = _networkTracker.UpdateBranch(state.Branch);
                    if (_config.Debug)
                    {
                        var stepEvents = state.Branch.Events.OfType<StepExecutionEvent>().ToList();
                        Console.WriteLine($"  📊 Network state: {trackResult.Value} events reified ({stepEvents.Count} steps tracked)");
                        foreach (var stepEvt in stepEvents.TakeLast(5))
                        {
                            var status = stepEvt.Success ? "✓" : "✗";
                            Console.WriteLine($"      {status} [{stepEvt.TokenName}] {stepEvt.Description} ({stepEvt.DurationMs}ms)");
                        }
                    }
                }

                // Track capability usage for self-improvement
                var duration = DateTime.UtcNow - startTime;
                if (_capabilityRegistry != null)
                {
                    var execResult = AutonomySubsystem.CreateCapabilityPlanExecutionResult(success, duration, dsl);
                    await _capabilityRegistry.UpdateCapabilityAsync("pipeline_execution", execResult);
                }

                // Update global workspace with execution result
                _globalWorkspace?.AddItem(
                    $"DSL Executed: {dsl[..Math.Min(100, dsl.Length)]}\nDuration: {duration.TotalSeconds:F2}s",
                    WorkspacePriority.Normal,
                    "dsl-execution",
                    new List<string> { "dsl", "pipeline", success ? "success" : "failure" });

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n  ✓ Pipeline completed");
                Console.ResetColor();

                // Get last reasoning output
                var lastReasoning = state.Branch.Events.OfType<PipelineReasoningStep>().LastOrDefault();
                if (lastReasoning != null)
                {
                    Console.WriteLine($"\n{lastReasoning.State.Text}");
                    await SayWithVoiceAsync(lastReasoning.State.Text);
                }
                else if (!string.IsNullOrEmpty(state.Output))
                {
                    Console.WriteLine($"\n{state.Output}");
                    await SayWithVoiceAsync(state.Output);
                }
            }
            else
            {
                Console.WriteLine("  ⚠ Cannot execute DSL: LLM or embeddings not available");
            }
        }
        catch (Exception ex)
        {
            // Track failure for self-improvement
            if (_capabilityRegistry != null)
            {
                var execResult = AutonomySubsystem.CreateCapabilityPlanExecutionResult(false, TimeSpan.Zero, dsl);
                await _capabilityRegistry.UpdateCapabilityAsync("pipeline_execution", execResult);
            }

            Console.ForegroundColor = ConsoleColor.Red;
            _output.WriteError($"DSL execution failed: {ex.Message}");
            Console.ResetColor();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MULTI-MODEL ORCHESTRATION & DIVIDE-AND-CONQUER HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Generates text using multi-model orchestration if available, falling back to single model.
    /// The orchestrator automatically routes to specialized models (coder, reasoner, summarizer)
    /// based on prompt content analysis.
    /// </summary>
    private async Task<string> GenerateWithOrchestrationAsync(string prompt, CancellationToken ct = default)
    {
        if (_orchestratedModel != null)
        {
            return await _orchestratedModel.GenerateTextAsync(prompt, ct);
        }

        if (_chatModel != null)
        {
            return await _chatModel.GenerateTextAsync(prompt, ct);
        }

        return "[error] No LLM available";
    }

    /// <summary>
    /// Processes large text input using divide-and-conquer parallel processing.
    /// Automatically chunks the input, processes in parallel, and merges results.
    /// </summary>
    /// <param name="task">The task instruction (e.g., "Summarize:", "Analyze:", "Extract key points:")</param>
    /// <param name="largeInput">The large text input to process</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Merged result from all chunk processing</returns>
    public async Task<string> ProcessLargeInputAsync(string task, string largeInput, CancellationToken ct = default)
    {
        // Use divide-and-conquer if available and input is large enough
        if (_divideAndConquer != null && largeInput.Length > 2000)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  [D&C] Processing large input ({largeInput.Length} chars) in parallel...");
            Console.ResetColor();

            var chunks = _divideAndConquer.DivideIntoChunks(largeInput);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  [D&C] Split into {chunks.Count} chunks");
            Console.ResetColor();

            var result = await _divideAndConquer.ExecuteAsync(task, chunks, ct);

            return result.Match(
                success =>
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("  [D&C] Parallel processing completed");
                    Console.ResetColor();
                    return success;
                },
                error =>
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  [D&C] Error: {error}");
                    Console.ResetColor();
                    // Fall back to direct processing
                    return GenerateWithOrchestrationAsync($"{task}\n\n{largeInput}", ct).Result;
                });
        }

        // For smaller inputs, use direct orchestration
        return await GenerateWithOrchestrationAsync($"{task}\n\n{largeInput}", ct);
    }

    /// <summary>
    /// Gets the current orchestration metrics showing model usage statistics.
    /// </summary>
    public IReadOnlyDictionary<string, PerformanceMetrics>? GetOrchestrationMetrics()
    {
        if (_orchestratedModel != null)
        {
            // Access through the builder's underlying orchestrator
            return null; // Would need to expose metrics from OrchestratedChatModel
        }

        return _divideAndConquer?.GetMetrics();
    }

    /// <summary>
    /// Checks if multi-model orchestration is enabled and available.
    /// </summary>
    public bool IsMultiModelEnabled => _orchestratedModel != null;

    /// <summary>
    /// Checks if divide-and-conquer processing is available.
    /// </summary>
    public bool IsDivideAndConquerEnabled => _divideAndConquer != null;

    //
    // AUTONOMY DELEGATES (methods moved to AutonomySubsystem)
    //

    private Task InitializeAutonomousCoordinatorAsync()
        => _autonomySub.InitializeAutonomousCoordinatorAsync();

    private Task<string> SelfExecCommandAsync(string subCommand)
        => _autonomySub.SelfExecCommandAsync(subCommand);

    private Task<string> SubAgentCommandAsync(string subCommand)
        => _autonomySub.SubAgentCommandAsync(subCommand);

    private Task<string> EpicCommandAsync(string subCommand)
        => _autonomySub.EpicCommandAsync(subCommand);

    private Task<string> GoalCommandAsync(string subCommand)
        => _autonomySub.GoalCommandAsync(subCommand);

    private Task<string> DelegateCommandAsync(string taskDescription)
        => _autonomySub.DelegateCommandAsync(taskDescription);

    private Task<string> SelfModelCommandAsync(string subCommand)
        => _autonomySub.SelfModelCommandAsync(subCommand);

    private Task<string> EvaluateCommandAsync(string subCommand)
        => _autonomySub.EvaluateCommandAsync(subCommand);

    // Push Mode commands (moved to AutonomySubsystem)
    private Task<string> ApproveIntentionAsync(string arg)
        => _autonomySub.ApproveIntentionAsync(arg);

    private Task<string> RejectIntentionAsync(string arg)
        => _autonomySub.RejectIntentionAsync(arg);

    private string ListPendingIntentions()
        => _autonomySub.ListPendingIntentions();

    private string PausePushMode()
        => _autonomySub.PausePushMode();

    private string ResumePushMode()
        => _autonomySub.ResumePushMode();

    //
    //  COGNITIVE DELEGATES  Emergent Behavior Commands (logic in CognitiveSubsystem)
    //

    private Task<string> EmergenceCommandAsync(string topic)
        => ((CognitiveSubsystem)_cognitiveSub).EmergenceCommandAsync(topic);

    private Task<string> DreamCommandAsync(string topic)
        => ((CognitiveSubsystem)_cognitiveSub).DreamCommandAsync(topic);

    private Task<string> IntrospectCommandAsync(string focus)
        => ((CognitiveSubsystem)_cognitiveSub).IntrospectCommandAsync(focus);

    // Thought commands (moved to MemorySubsystem)
    private Task<string> SaveThoughtCommandAsync(string argument)
        => _memorySub.SaveThoughtCommandAsync(argument);

    private void TrackLastThought(string content)
        => _memorySub.TrackLastThought(content);

    // Code Self-Perception commands (moved to AutonomySubsystem)
    private Task<string> SaveCodeCommandAsync(string argument)
        => _autonomySub.SaveCodeCommandAsync(argument);

    private Task<string> ReadMyCodeCommandAsync(string filePath)
        => _autonomySub.ReadMyCodeCommandAsync(filePath);

    private Task<string> SearchMyCodeCommandAsync(string query)
        => _autonomySub.SearchMyCodeCommandAsync(query);

    private Task<string> AnalyzeCodeCommandAsync(string input)
        => _autonomySub.AnalyzeCodeCommandAsync(input);

    // Index commands (moved to AutonomySubsystem)
    private Task<string> ReindexFullAsync()
        => _autonomySub.ReindexFullAsync();

    private Task<string> ReindexIncrementalAsync()
        => _autonomySub.ReindexIncrementalAsync();

    private Task<string> IndexSearchAsync(string query)
        => _autonomySub.IndexSearchAsync(query);

    private Task<string> GetIndexStatsAsync()
        => _autonomySub.GetIndexStatsAsync();
    //
    //  COGNITIVE DELEGATES  AGI Subsystem Methods (logic in CognitiveSubsystem)
    //

    private void RecordInteractionForLearning(string input, string response)
        => ((CognitiveSubsystem)_cognitiveSub).RecordInteractionForLearning(input, response);

    private void RecordCognitiveEvent(string input, string response, List<ToolExecution>? tools)
        => ((CognitiveSubsystem)_cognitiveSub).RecordCognitiveEvent(input, response, tools);

    private void UpdateSelfAssessment(string input, string response, List<ToolExecution>? tools)
        => ((CognitiveSubsystem)_cognitiveSub).UpdateSelfAssessment(input, response, tools);

    private string GetAgiStatus()
        => ((CognitiveSubsystem)_cognitiveSub).GetAgiStatus();

    private Task<string> RunCouncilDebateAsync(string topic)
        => ((CognitiveSubsystem)_cognitiveSub).RunCouncilDebateAsync(topic);

    private string GetIntrospectionReport()
        => ((CognitiveSubsystem)_cognitiveSub).GetIntrospectionReport();

    private string GetWorldModelStatus()
        => ((CognitiveSubsystem)_cognitiveSub).GetWorldModelStatus();

    private Task<string> RunAgentCoordinationAsync(string goalDescription)
        => ((CognitiveSubsystem)_cognitiveSub).RunAgentCoordinationAsync(goalDescription);

    private string GetExperienceBufferStatus()
        => ((CognitiveSubsystem)_cognitiveSub).GetExperienceBufferStatus();

    private string GetPromptOptimizerStatus()
        => ((CognitiveSubsystem)_cognitiveSub).GetPromptOptimizerStatus();

    private static string TruncateText(string text, int maxLength)
        => CognitiveSubsystem.TruncateText(text, maxLength);


}

// ═══════════════════════════════════════════════════════════════════════════
// SUPPORTING TYPES
// ═══════════════════════════════════════════════════════════════════════════
