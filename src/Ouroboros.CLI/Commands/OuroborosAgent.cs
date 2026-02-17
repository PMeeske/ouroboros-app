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
    private string? _lastUserInput; // Track for outcome recording
    private DateTime _lastInteractionStart;
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

    /// <summary>
    /// Uses LLM to integrate tool results naturally into a conversational response.
    /// Converts raw tool output (tables, JSON, etc.) into natural language.
    /// </summary>
    private async Task<string> SanitizeToolResultsAsync(string originalResponse, string toolResults)
    {
        if (_chatModel == null || string.IsNullOrWhiteSpace(toolResults))
        {
            return $"{originalResponse}\n\n{toolResults}";
        }

        try
        {
            string prompt = PromptResources.ToolIntegration(originalResponse, toolResults);

            string sanitized = await _chatModel.GenerateTextAsync(prompt);
            return string.IsNullOrWhiteSpace(sanitized) ? $"{originalResponse}\n\n{toolResults}" : sanitized;
        }
        catch
        {
            // Fallback to raw output if sanitization fails
            return $"{originalResponse}\n\n{toolResults}";
        }
    }

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

            var ssml = $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='{(_config.Culture ?? "en-US")}'>
    <voice name='{voiceName}'>
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
        IEmbodimentSubsystem embodiment)
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

        _voice = _voiceSub.Service;
        _allSubsystems = [_voiceSub, _modelsSub, _toolsSub, _memorySub, _cognitiveSub, _autonomySub, _embodimentSub];

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
            new EmbodimentSubsystem())
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
        };

        // ── Phase 1: Infrastructure (standalone) ──
        if (_config.Voice)
            await _voice.InitializeAsync();

        _voiceSub.SpeakWithSapiFunc = SpeakWithSapiAsync;
        await _voiceSub.InitializeAsync(ctx);

        // ── Phase 2: Models (standalone) ──
        await _modelsSub.InitializeAsync(ctx);

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

        // ── Phase 8: Cross-subsystem wiring (mediator orchestration) ──
        WireCrossSubsystemDependencies();

        // ── Phase 9: Post-init actions ──
        _isInitialized = true;
        _output.FlushInitSummary();
        if (_config.Verbosity == OutputVerbosity.Verbose)
            PrintQuickHelp();

        // AGI warmup - prime the model with examples for autonomous operation
        await PerformAgiWarmupAsync();

        // Enforce policies if self-modification is enabled
        if (_config.EnableSelfModification)
            await EnforceGovernancePoliciesAsync();

        // Start listening for voice input if enabled via CLI
        if (_config.Listen)
        {
            _output.WriteSystem("Voice listening enabled via --listen flag");
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
        WireAutonomousMindDelegates();

        // ── Autonomous Coordinator ──
        WireAutonomousCoordinatorAsync().GetAwaiter().GetResult();

        // ── Self-Execution ──
        if (_config.EnableMind)
            WireSelfExecution();

        // ── Self-Assembly callbacks ──
        WireSelfAssemblyCallbacks();

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
    private void WireAutonomousMindDelegates()
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

        // Proactive message events
        _autonomousMind.OnProactiveMessage += async (msg) =>
        {
            var thoughtContent = msg.TrimStart();
            if (thoughtContent.StartsWith("💡") || thoughtContent.StartsWith("💬") ||
                thoughtContent.StartsWith("🤔") || thoughtContent.StartsWith("💭"))
                thoughtContent = thoughtContent[2..].Trim();
            TrackLastThought(thoughtContent);

            string savedInput;
            lock (_inputLock) { savedInput = _currentInputBuffer.ToString(); }
            if (!string.IsNullOrEmpty(savedInput))
                Console.WriteLine();
            _output.WriteDebug($"💭 {msg}");
            try { await _voice.WhisperAsync(msg); } catch { }
            if (_isInConversationLoop)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("\n  You: ");
                Console.ResetColor();
                if (!string.IsNullOrEmpty(savedInput))
                    Console.Write(savedInput);
            }
        };

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
            await PersistThoughtAsync(innerThought, "autonomous_thinking");
        };
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
    /// Wires self-assembly LLM code generator and approval callback.
    /// </summary>
    private void WireSelfAssemblyCallbacks()
    {
        if (_selfAssemblyEngine == null) return;

        if (_llm != null)
        {
            _selfAssemblyEngine.SetCodeGenerator(async blueprint =>
                await GenerateNeuronCodeAsync(blueprint));
        }

        _selfAssemblyEngine.SetApprovalCallback(async proposal =>
            await RequestSelfAssemblyApprovalAsync(proposal));

        _selfAssemblyEngine.NeuronAssembled += OnNeuronAssembled;
        _selfAssemblyEngine.AssemblyFailed += OnAssemblyFailed;
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

        _immersivePersona.AutonomousThought += (_, e) =>
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"\n  [inner thought] {e.Thought.Content}");
            Console.ResetColor();
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
        _autonomousMind.OnDiscovery += (query, fact) =>
        {
            System.Diagnostics.Debug.WriteLine($"[Discovery] {query}: {fact}");
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

    /// <summary>
    /// Gets the language name for a given culture code.
    /// </summary>
    private string GetLanguageName(string culture)
    {
        return culture.ToLowerInvariant() switch
        {
            "de-de" => "German",
            "fr-fr" => "French",
            "es-es" => "Spanish",
            "it-it" => "Italian",
            "pt-br" => "Portuguese (Brazilian)",
            "pt-pt" => "Portuguese (European)",
            "nl-nl" => "Dutch",
            "sv-se" => "Swedish",
            "ja-jp" => "Japanese",
            "zh-cn" => "Chinese (Simplified)",
            "zh-tw" => "Chinese (Traditional)",
            "ko-kr" => "Korean",
            "ru-ru" => "Russian",
            "pl-pl" => "Polish",
            "tr-tr" => "Turkish",
            "ar-sa" => "Arabic",
            "he-il" => "Hebrew",
            "th-th" => "Thai",
            _ => culture
        };
    }

    /// <summary>
    /// Gets the default Azure TTS voice name for a given culture code.
    /// </summary>
    private static string GetDefaultVoiceForCulture(string? culture)
    {
        return culture?.ToLowerInvariant() switch
        {
            "de-de" => "de-DE-KatjaNeural",
            "fr-fr" => "fr-FR-DeniseNeural",
            "es-es" => "es-ES-ElviraNeural",
            "it-it" => "it-IT-ElsaNeural",
            "pt-br" => "pt-BR-FranciscaNeural",
            "pt-pt" => "pt-PT-RaquelNeural",
            "nl-nl" => "nl-NL-ColetteNeural",
            "sv-se" => "sv-SE-SofieNeural",
            "ja-jp" => "ja-JP-NanamiNeural",
            "zh-cn" => "zh-CN-XiaoxiaoNeural",
            "zh-tw" => "zh-TW-HsiaoChenNeural",
            "ko-kr" => "ko-KR-SunHiNeural",
            "ru-ru" => "ru-RU-SvetlanaNeural",
            "pl-pl" => "pl-PL-ZofiaNeural",
            "tr-tr" => "tr-TR-EmelNeural",
            "ar-sa" => "ar-SA-ZariyahNeural",
            "he-il" => "he-IL-HilaNeural",
            "th-th" => "th-TH-PremwadeeNeural",
            _ => "en-US-AvaMultilingualNeural"
        };
    }

    /// <summary>
    /// Gets the effective TTS voice, considering culture override.
    /// If culture is set and voice wasn't explicitly changed from default, use culture-specific voice.
    /// </summary>
    private string GetEffectiveVoice()
    {
        // If user didn't explicitly set a voice (still using default), auto-select based on culture
        if (_config.TtsVoice == "en-US-AvaMultilingualNeural" &&
            !string.IsNullOrEmpty(_config.Culture) &&
            _config.Culture != "en-US")
        {
            return GetDefaultVoiceForCulture(_config.Culture);
        }

        return _config.TtsVoice;
    }

    /// <summary>
    /// Translates a thought to the target language if culture is specified.
    /// </summary>
    private async Task<string> TranslateThoughtIfNeededAsync(string thought)
    {
        // Only translate if a non-English culture is set
        if (string.IsNullOrEmpty(_config.Culture) || _config.Culture == "en-US" || _llm == null)
        {
            return thought;
        }

        try
        {
            var languageName = GetLanguageName(_config.Culture);
            var translationPrompt = $@"TASK: Translate to {languageName}.
INPUT: {thought}
OUTPUT (translation only, no explanations, no JSON, no metadata):";

            var (translated, _) = await _llm.GenerateWithToolsAsync(translationPrompt);

            // Clean up any extra formatting the LLM might add
            var result = translated?.Trim() ?? thought;

            // Remove common LLM artifacts
            if (result.StartsWith("\"") && result.EndsWith("\""))
                result = result[1..^1];
            if (result.Contains("```"))
                result = result.Split("```")[0].Trim();
            if (result.Contains("{") && result.Contains("}"))
                result = result.Split("{")[0].Trim();

            return string.IsNullOrEmpty(result) ? thought : result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Thought Translation] Error: {ex.Message}");
            return thought;
        }
    }

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
            var ssml = useFriendlyStyle
                ? $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis'
                    xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='{culture}'>
                    <voice name='{azureVoice}'>
                        <mstts:express-as style='friendly' styledegree='0.8'>
                            <prosody rate='-5%' pitch='+8%' volume='+3%'>
                                <mstts:audioduration value='1.1'/>
                                {System.Security.SecurityElement.Escape(text)}
                            </prosody>
                        </mstts:express-as>
                        <mstts:audioeffect type='eq_car'/>
                    </voice>
                </speak>"
                : $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='{culture}'>
                    <voice name='{azureVoice}'>
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
            _agiWarmup = new AgiWarmup(
                thinkFunction: _autonomousMind?.ThinkFunction,
                searchFunction: _autonomousMind?.SearchFunction,
                executeToolFunction: _autonomousMind?.ExecuteToolFunction,
                selfIndexer: _selfIndexer,
                toolRegistry: _tools);

            if (_config.Verbosity == OutputVerbosity.Verbose)
            {
                _output.WriteDebug("Warming up AGI systems...");
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
                {
                    _output.WriteDebug($"AGI warmup complete in {result.Duration.TotalSeconds:F1}s");

                    // Print initial thought if available
                    if (!string.IsNullOrEmpty(result.WarmupThought))
                    {
                        var translatedThought = await TranslateThoughtIfNeededAsync(result.WarmupThought);
                        _output.WriteDebug($"💭 Initial thought: \"{translatedThought}\"");
                    }
                }
                else
                {
                    _output.WriteWarning($"AGI warmup limited: {result.Error ?? "Some features unavailable"}");
                }
            }
        }
        catch (Exception ex)
        {
            _output.WriteDebug($"AGI warmup skipped: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates neuron code from a blueprint using LLM.
    /// </summary>
    private async Task<string> GenerateNeuronCodeAsync(NeuronBlueprint blueprint)
    {
        if (_llm == null)
        {
            throw new InvalidOperationException("LLM not available for code generation");
        }

        var prompt = PromptResources.NeuronCodeGen(
            blueprint.Name,
            blueprint.Description,
            blueprint.Rationale,
            blueprint.Type.ToString(),
            string.Join(", ", blueprint.SubscribedTopics),
            string.Join(", ", blueprint.Capabilities),
            string.Join("\n", blueprint.MessageHandlers.Select(h => $"- Topic '{h.TopicPattern}': {h.HandlingLogic} (responds={h.SendsResponse}, broadcasts={h.BroadcastsResult})")),
            blueprint.HasAutonomousTick ? $"AUTONOMOUS TICK: {blueprint.TickBehaviorDescription}" : "No autonomous tick behavior");

        var response = await _llm.InnerModel.GenerateTextAsync(prompt, CancellationToken.None);

        // Extract code from markdown if present
        var code = response;
        if (response.Contains("```csharp"))
        {
            var start = response.IndexOf("```csharp") + 9;
            var end = response.IndexOf("```", start);
            if (end > start)
            {
                code = response[start..end].Trim();
            }
        }
        else if (response.Contains("```"))
        {
            var start = response.IndexOf("```") + 3;
            var end = response.IndexOf("```", start);
            if (end > start)
            {
                code = response[start..end].Trim();
            }
        }

        // Ensure required using statements
        if (!code.Contains("using Ouroboros.Domain.Autonomous"))
        {
            code = "using System;\nusing System.Collections.Generic;\nusing System.Threading;\nusing System.Threading.Tasks;\nusing Ouroboros.Domain.Autonomous;\n\n" + code;
        }

        return code;
    }

    /// <summary>
    /// Requests user approval for a self-assembly proposal.
    /// </summary>
    private async Task<bool> RequestSelfAssemblyApprovalAsync(
        AssemblyProposal proposal)
    {
        var blueprint = proposal.Blueprint;

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║           🧬 SELF-ASSEMBLY PROPOSAL                           ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        Console.ResetColor();

        Console.WriteLine($"\n  Neuron: {blueprint.Name}");
        Console.WriteLine($"  Description: {blueprint.Description}");
        Console.WriteLine($"  Rationale: {blueprint.Rationale}");
        Console.WriteLine($"  Type: {blueprint.Type}");
        Console.WriteLine($"  Topics: {string.Join(", ", blueprint.SubscribedTopics)}");
        Console.WriteLine($"  Capabilities: {string.Join(", ", blueprint.Capabilities)}");
        Console.WriteLine($"  Confidence: {blueprint.ConfidenceScore:P0}");

        Console.ForegroundColor = proposal.Validation.SafetyScore >= 0.8
            ? ConsoleColor.Green
            : ConsoleColor.Yellow;
        Console.WriteLine($"  Safety Score: {proposal.Validation.SafetyScore:P0}");
        Console.ResetColor();

        if (proposal.Validation.Violations.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  Violations: {string.Join(", ", proposal.Validation.Violations)}");
            Console.ResetColor();
        }

        if (proposal.Validation.Warnings.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  Warnings: {string.Join(", ", proposal.Validation.Warnings)}");
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.Write("  Approve this self-assembly? [y/N]: ");

        var response = await Task.Run(() => Console.ReadLine());
        return response?.Trim().ToLowerInvariant() is "y" or "yes";
    }

    private void OnNeuronAssembled(object? sender, NeuronAssembledEvent e)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  🧬 SELF-ASSEMBLED: {e.NeuronName} (Type: {e.NeuronType.Name})");
        Console.ResetColor();

        // Create and register the neuron instance
        if (_selfAssemblyEngine is not null)
        {
            var instanceResult = _selfAssemblyEngine.CreateNeuronInstance(e.NeuronName);
            if (instanceResult.IsSuccess && instanceResult.Value is Neuron neuron)
            {
                _autonomousCoordinator?.Network?.RegisterNeuron(neuron);
                neuron.Start();
            }
        }

        // Log to conversation
        _conversationHistory.Add($"[SYSTEM] Self-assembled neuron: {e.NeuronName}");
    }

    private void OnAssemblyFailed(object? sender, AssemblyFailedEvent e)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  ⚠ Assembly failed for '{e.NeuronName}': {e.Reason}");
        Console.ResetColor();
    }

    /// <summary>
    /// Analyzes the system for capability gaps and proposes new neurons.
    /// Can be called periodically or on-demand.
    /// </summary>
    public async Task<IReadOnlyList<NeuronBlueprint>> AnalyzeAndProposeNeuronsAsync(CancellationToken ct = default)
    {
        if (_blueprintAnalyzer == null || _selfAssemblyEngine == null)
        {
            return [];
        }

        try
        {
            // Get recent messages from the network
            var recentMessages = new List<NeuronMessage>();
            // In a real implementation, we'd query the message history

            var gaps = await _blueprintAnalyzer.AnalyzeGapsAsync(recentMessages, ct);
            var blueprints = new List<NeuronBlueprint>();

            foreach (var gap in gaps.Where(g => g.Importance >= 0.6))
            {
                var blueprint = await _blueprintAnalyzer.GenerateBlueprintForGapAsync(gap, ct);
                if (blueprint != null)
                {
                    blueprints.Add(blueprint);
                }
            }

            return blueprints;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SelfAssembly] Analysis failed: {ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// Attempts to assemble a neuron from a blueprint.
    /// </summary>
    public async Task<Neuron?> AssembleNeuronAsync(NeuronBlueprint blueprint, CancellationToken ct = default)
    {
        if (_selfAssemblyEngine == null)
        {
            throw new InvalidOperationException("Self-assembly engine not initialized");
        }

        var proposalResult = await _selfAssemblyEngine.SubmitBlueprintAsync(blueprint);
        if (!proposalResult.IsSuccess)
        {
            return null;
        }

        // Wait for the pipeline to complete (async in background)
        await Task.Delay(100, ct); // Small delay to allow pipeline to start

        // Check if deployed
        var neurons = _selfAssemblyEngine.GetAssembledNeurons();
        if (neurons.TryGetValue(blueprint.Name, out var neuronType))
        {
            var instance = _selfAssemblyEngine.CreateNeuronInstance(blueprint.Name);
            return instance.IsSuccess ? instance.Value : null;
        }

        return null;
    }

    /// <summary>
    /// Builds context from persistent thoughts for injection into prompts.
    /// </summary>
    private string BuildPersistentThoughtContext()
    {
        if (_persistentThoughts.Count == 0) return "";

        var sb = new StringBuilder();
        sb.AppendLine("\n[PERSISTENT MEMORY - Your thoughts from previous sessions]");

        // Group by type and show the most relevant/recent ones
        var recentThoughts = _persistentThoughts
            .OrderByDescending(t => t.Timestamp)
            .Take(10);

        foreach (var thought in recentThoughts)
        {
            var age = DateTime.UtcNow - thought.Timestamp;
            var ageStr = age.TotalHours < 1 ? $"{age.TotalMinutes:F0}m ago"
                       : age.TotalDays < 1 ? $"{age.TotalHours:F0}h ago"
                       : $"{age.TotalDays:F0}d ago";

            sb.AppendLine($"  [{thought.Type}] ({ageStr}): {thought.Content}");
        }

        sb.AppendLine("[END PERSISTENT MEMORY]\n");
        return sb.ToString();
    }


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

    /// <summary>
    /// Runs in non-interactive mode for piping, batch processing, or single command execution.
    /// Supports Unix-style | piping within commands to chain agent operations.
    /// </summary>
    private async Task RunNonInteractiveModeAsync()
    {
        var commands = new List<string>();

        // Collect commands from various sources
        if (!string.IsNullOrWhiteSpace(_config.ExecCommand))
        {
            // Single exec command (may contain | for internal piping)
            commands.Add(_config.ExecCommand);
        }
        else if (!string.IsNullOrWhiteSpace(_config.BatchFile))
        {
            // Batch file mode
            if (!File.Exists(_config.BatchFile))
            {
                OutputError($"Batch file not found: {_config.BatchFile}");
                return;
            }
            commands.AddRange(await File.ReadAllLinesAsync(_config.BatchFile));
        }
        else if (_config.PipeMode || Console.IsInputRedirected)
        {
            // Pipe mode - read from stdin
            string? line;
            while ((line = Console.ReadLine()) != null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    commands.Add(line);
            }
        }

        // Process each command
        string? lastOutput = null;
        foreach (var rawCmd in commands)
        {
            var cmd = rawCmd.Trim();
            if (string.IsNullOrWhiteSpace(cmd) || cmd.StartsWith("#")) continue; // Skip empty/comments

            // Handle internal piping: "ask question | summarize | remember"
            var pipeSegments = ParsePipeSegments(cmd);

            foreach (var segment in pipeSegments)
            {
                var commandToRun = segment.Trim();

                // Substitute $PIPE or $_ with last output
                if (lastOutput != null)
                {
                    commandToRun = commandToRun
                        .Replace("$PIPE", lastOutput)
                        .Replace("$_", lastOutput);

                    // If segment starts with |, prepend last output as context
                    if (segment.TrimStart().StartsWith("|"))
                    {
                        commandToRun = $"{lastOutput}\n---\n{commandToRun.TrimStart().TrimStart('|').Trim()}";
                    }
                }

                if (string.IsNullOrWhiteSpace(commandToRun)) continue;

                try
                {
                    var response = await ProcessInputAsync(commandToRun);
                    lastOutput = response;
                    OutputResponse(commandToRun, response);
                }
                catch (Exception ex)
                {
                    OutputError($"Error processing '{commandToRun}': {ex.Message}");
                    if (_config.ExitOnError)
                        return;
                    lastOutput = null;
                }
            }
        }
    }

    /// <summary>
    /// Parses pipe segments from a command string.
    /// Handles escaping and quoted strings containing |.
    /// </summary>
    private static List<string> ParsePipeSegments(string command)
    {
        var segments = new List<string>();
        var current = new StringBuilder();
        bool inQuote = false;
        char quoteChar = '"';

        for (int i = 0; i < command.Length; i++)
        {
            char c = command[i];

            // Handle quotes
            if ((c == '"' || c == '\'') && (i == 0 || command[i - 1] != '\\'))
            {
                if (!inQuote)
                {
                    inQuote = true;
                    quoteChar = c;
                }
                else if (c == quoteChar)
                {
                    inQuote = false;
                }
                current.Append(c);
                continue;
            }

            // Handle pipe outside quotes
            if (c == '|' && !inQuote)
            {
                var segment = current.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(segment))
                    segments.Add(segment);
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        // Add final segment
        var final = current.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(final))
            segments.Add(final);

        return segments;
    }

    /// <summary>
    /// Outputs a response in the configured format (plain text or JSON).
    /// </summary>
    private void OutputResponse(string command, string response)
    {
        if (_config.JsonOutput)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                command,
                response,
                timestamp = DateTime.UtcNow,
                success = true
            });
            Console.WriteLine(json);
        }
        else
        {
            Console.WriteLine(response);
        }
    }

    /// <summary>
    /// Outputs an error in the configured format.
    /// </summary>
    private void OutputError(string message)
    {
        if (_config.JsonOutput)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                error = message,
                timestamp = DateTime.UtcNow,
                success = false
            });
            Console.WriteLine(json);
        }
        else
        {
            Console.Error.WriteLine($"ERROR: {message}");
        }
    }

    /// <summary>
    /// Processes input with support for | piping syntax.
    /// Allows chaining commands like: "ask what is AI | summarize | remember"
    /// Also detects and executes pipe commands in model responses.
    /// </summary>
    public async Task<string> ProcessInputWithPipingAsync(string input, int maxPipeDepth = 5)
    {
        // Check if input contains pipe operators (outside quotes)
        var segments = ParsePipeSegments(input);

        if (segments.Count <= 1)
        {
            // No piping, process normally
            var response = await ProcessInputAsync(input);

            // Check if model response contains a pipe command to execute
            response = await ExecuteModelPipeCommandsAsync(response, maxPipeDepth);

            return response;
        }

        // Execute pipe chain
        string? lastOutput = null;
        var allOutputs = new List<string>();

        for (int i = 0; i < segments.Count && i < maxPipeDepth; i++)
        {
            var segment = segments[i].Trim();
            if (string.IsNullOrWhiteSpace(segment)) continue;

            // Substitute previous output into current command
            var commandToRun = segment;
            if (lastOutput != null)
            {
                // Replace $PIPE or $_ placeholders
                commandToRun = commandToRun
                    .Replace("$PIPE", lastOutput)
                    .Replace("$_", lastOutput);

                // If no placeholder, prepend as context
                if (!segment.Contains("$PIPE") && !segment.Contains("$_"))
                {
                    commandToRun = $"Given this context:\n---\n{lastOutput}\n---\n{segment}";
                }
            }

            try
            {
                lastOutput = await ProcessInputAsync(commandToRun);
                allOutputs.Add($"[Step {i + 1}: {segment[..Math.Min(30, segment.Length)]}...]\n{lastOutput}");
            }
            catch (Exception ex)
            {
                allOutputs.Add($"[Step {i + 1} ERROR: {ex.Message}]");
                break;
            }
        }

        // Return final output (or combined if debug)
        return lastOutput ?? string.Join("\n\n", allOutputs);
    }

    /// <summary>
    /// Detects and executes pipe commands embedded in model responses.
    /// Looks for patterns like: [PIPE: command1 | command2]
    /// </summary>
    private async Task<string> ExecuteModelPipeCommandsAsync(string response, int maxDepth)
    {
        if (maxDepth <= 0) return response;

        // Look for [PIPE: ...] or ```pipe ... ``` blocks in response
        var pipePattern = new Regex(@"\[PIPE:\s*(.+?)\]|\`\`\`pipe\s*\n(.+?)\n\`\`\`", RegexOptions.Singleline);
        var matches = pipePattern.Matches(response);

        if (matches.Count == 0) return response;

        var result = response;
        foreach (Match match in matches)
        {
            var pipeCommand = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            if (string.IsNullOrWhiteSpace(pipeCommand)) continue;

            try
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"  🔗 Executing pipe: {pipeCommand[..Math.Min(50, pipeCommand.Length)]}...");
                Console.ResetColor();

                var pipeResult = await ProcessInputWithPipingAsync(pipeCommand.Trim(), maxDepth - 1);

                // Replace the pipe command with its result
                result = result.Replace(match.Value, $"\n📤 Pipe Result:\n{pipeResult}\n");
            }
            catch (Exception ex)
            {
                result = result.Replace(match.Value, $"\n❌ Pipe Error: {ex.Message}\n");
            }
        }

        return result;
    }

    /// <summary>
    /// Processes user input and returns a response.
    /// </summary>
    public async Task<string> ProcessInputAsync(string input)
    {
        // Parse for action commands
        var action = ParseAction(input);

        return action.Type switch
        {
            ActionType.Help => GetHelpText(),
            ActionType.ListSkills => await ListSkillsAsync(),
            ActionType.ListTools => ListTools(),
            ActionType.LearnTopic => await LearnTopicAsync(action.Argument),
            ActionType.CreateTool => await CreateToolAsync(action.Argument),
            ActionType.UseTool => await UseToolAsync(action.Argument, action.ToolInput),
            ActionType.RunSkill => await RunSkillAsync(action.Argument),
            ActionType.Suggest => await SuggestSkillsAsync(action.Argument),
            ActionType.Plan => await PlanAsync(action.Argument),
            ActionType.Execute => await ExecuteAsync(action.Argument),
            ActionType.Status => GetStatus(),
            ActionType.Mood => GetMood(),
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
            ActionType.Explain => ExplainDsl(action.Argument),
            ActionType.Test => await RunTestAsync(action.Argument),
            // Merged from ImmersiveMode and Skills mode
            ActionType.Consciousness => GetConsciousnessState(),
            ActionType.Tokens => GetDslTokens(),
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
            ActionType.CoordinatorCommand => ProcessCoordinatorCommand(input),
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

    /// <summary>
    /// Routes commands to the AutonomousCoordinator.
    /// </summary>
    private string ProcessCoordinatorCommand(string input)
    {
        if (_autonomousCoordinator == null)
            return "Push mode is not enabled. Start with --push to enable autonomous commands.";

        var handled = _autonomousCoordinator.ProcessCommand(input);
        return handled
            ? "" // Coordinator handles output via OnProactiveMessage
            : $"Unknown command: {input}. Use /help for available commands.";
    }

    private (ActionType Type, string Argument, string? ToolInput) ParseAction(string input)
    {
        var lower = input.ToLowerInvariant().Trim();

        // Handle thought input prefixed with [💭] - track, execute tools if needed, and acknowledge
        if (input.TrimStart().StartsWith("[💭]"))
        {
            var thought = input.TrimStart()[4..].Trim(); // Remove [💭] prefix
            TrackLastThought(thought);

            // === KEY INSIGHT: Auto-execute tools from thoughts ===
            // This is why auto-tool works: DIRECT invocation, not waiting for LLM
            _ = Task.Run(async () => await _toolsSub.ExecuteToolsFromThought(thought));

            return (ActionType.SaveThought, thought, null);
        }

        // Help
        if (lower is "help" or "?" or "commands")
            return (ActionType.Help, "", null);

        // Status
        if (lower is "status" or "state" or "stats")
            return (ActionType.Status, "", null);

        // Mood
        if (lower.Contains("how are you") || lower.Contains("how do you feel") || lower is "mood")
            return (ActionType.Mood, "", null);

        // List commands
        if (lower.StartsWith("list skill") || lower == "skills" || lower == "what skills")
            return (ActionType.ListSkills, "", null);

        if (lower.StartsWith("list tool") || lower == "tools" || lower == "what tools")
            return (ActionType.ListTools, "", null);

        // Learn
        if (lower.StartsWith("learn about "))
            return (ActionType.LearnTopic, input[12..].Trim(), null);
        if (lower.StartsWith("learn "))
            return (ActionType.LearnTopic, input[6..].Trim(), null);
        if (lower.StartsWith("research "))
            return (ActionType.LearnTopic, input[9..].Trim(), null);

        // Tool creation
        if (lower.StartsWith("create tool ") || lower.StartsWith("add tool "))
            return (ActionType.CreateTool, input.Split(' ', 3).Last(), null);
        if (lower.StartsWith("make a ") && lower.Contains("tool"))
            return (ActionType.CreateTool, ExtractToolName(input), null);

        // Tool usage
        if (lower.StartsWith("use ") && lower.Contains(" to "))
        {
            var parts = input[4..].Split(" to ", 2);
            return (ActionType.UseTool, parts[0].Trim(), parts.Length > 1 ? parts[1].Trim() : null);
        }
        if (lower.StartsWith("search for ") || lower.StartsWith("search "))
        {
            var query = lower.StartsWith("search for ") ? input[11..] : input[7..];
            return (ActionType.UseTool, "search", query.Trim());
        }

        // Run skill
        if (lower.StartsWith("run ") || lower.StartsWith("execute "))
            return (ActionType.RunSkill, input.Split(' ', 2).Last(), null);

        // Suggest
        if (lower.StartsWith("suggest "))
            return (ActionType.Suggest, input[8..].Trim(), null);

        // Plan
        if (lower.StartsWith("plan ") || lower.StartsWith("how would you "))
            return (ActionType.Plan, input.Split(' ', 2).Last(), null);

        // Execute with planning
        if (lower.StartsWith("do ") || lower.StartsWith("accomplish "))
            return (ActionType.Execute, input.Split(' ', 2).Last(), null);

        // Memory
        if (lower.StartsWith("remember "))
            return (ActionType.Remember, input[9..].Trim(), null);
        if (lower.StartsWith("recall ") || lower.StartsWith("what do you know about "))
        {
            var topic = lower.StartsWith("recall ") ? input[7..] : input[23..];
            return (ActionType.Recall, topic.Trim(), null);
        }

        // MeTTa query
        if (lower.StartsWith("query ") || lower.StartsWith("metta "))
            return (ActionType.Query, input.Split(' ', 2).Last(), null);

        // === UNIFIED CLI COMMANDS ===

        // Ask - single question mode
        if (lower.StartsWith("ask "))
            return (ActionType.Ask, input[4..].Trim(), null);

        // Pipeline - run a DSL pipeline
        if (lower.StartsWith("pipeline ") || lower.StartsWith("pipe "))
        {
            var arg = lower.StartsWith("pipeline ") ? input[9..] : input[5..];
            return (ActionType.Pipeline, arg.Trim(), null);
        }

        // Metta - direct MeTTa expression
        if (lower.StartsWith("!(") || lower.StartsWith("(") || lower.StartsWith("metta:"))
        {
            var expr = lower.StartsWith("metta:") ? input[6..] : input;
            return (ActionType.Metta, expr.Trim(), null);
        }

        // Orchestrator mode
        if (lower.StartsWith("orchestrate ") || lower.StartsWith("orch "))
        {
            var arg = lower.StartsWith("orchestrate ") ? input[12..] : input[5..];
            return (ActionType.Orchestrate, arg.Trim(), null);
        }

        // Network commands
        if (lower.StartsWith("network ") || lower == "network")
            return (ActionType.Network, input.Length > 8 ? input[8..].Trim() : "status", null);

        // DAG commands
        if (lower.StartsWith("dag ") || lower == "dag")
            return (ActionType.Dag, input.Length > 4 ? input[4..].Trim() : "show", null);

        // Affect/emotions
        if (lower.StartsWith("affect ") || lower.StartsWith("emotion"))
            return (ActionType.Affect, input.Split(' ', 2).Last(), null);

        // Environment
        if (lower.StartsWith("env ") || lower.StartsWith("environment"))
            return (ActionType.Environment, input.Split(' ', 2).Last(), null);

        // Maintenance
        if (lower.StartsWith("maintenance ") || lower.StartsWith("maintain"))
            return (ActionType.Maintenance, input.Split(' ', 2).Last(), null);

        // Policy
        if (lower.StartsWith("policy "))
            return (ActionType.Policy, input[7..].Trim(), null);

        // Explain DSL
        if (lower.StartsWith("explain "))
            return (ActionType.Explain, input[8..].Trim(), null);

        // Test
        if (lower.StartsWith("test ") || lower == "test")
            return (ActionType.Test, input.Length > 5 ? input[5..].Trim() : "", null);

        // Consciousness state
        if (lower is "consciousness" or "conscious" or "inner" or "self")
            return (ActionType.Consciousness, "", null);

        // DSL Tokens (from Skills mode)
        if (lower is "tokens" or "t")
            return (ActionType.Tokens, "", null);

        // Fetch/learn from arXiv (from Skills mode)
        if (lower.StartsWith("fetch "))
            return (ActionType.Fetch, input[6..].Trim(), null);

        // Process large text with divide-and-conquer (from Skills mode)
        if (lower.StartsWith("process ") || lower.StartsWith("dc "))
        {
            var arg = lower.StartsWith("process ") ? input[8..].Trim() : input[3..].Trim();
            return (ActionType.Process, arg, null);
        }

        // === SELF-EXECUTION AND SUB-AGENT COMMANDS ===

        // Self-execution commands
        if (lower.StartsWith("selfexec ") || lower.StartsWith("self-exec ") || lower == "selfexec")
        {
            var arg = lower.StartsWith("selfexec ") ? input[9..].Trim()
                : lower.StartsWith("self-exec ") ? input[10..].Trim() : "";
            return (ActionType.SelfExec, arg, null);
        }

        // Sub-agent commands
        if (lower.StartsWith("subagent ") || lower.StartsWith("sub-agent ") || lower == "subagents" || lower == "agents")
        {
            var arg = lower.StartsWith("subagent ") ? input[9..].Trim()
                : lower.StartsWith("sub-agent ") ? input[10..].Trim() : "";
            return (ActionType.SubAgent, arg, null);
        }

        // Epic/project orchestration
        if (lower.StartsWith("epic ") || lower == "epic" || lower == "epics")
        {
            var arg = lower.StartsWith("epic ") ? input[5..].Trim() : "";
            return (ActionType.Epic, arg, null);
        }

        // Goal queue management
        if (lower.StartsWith("goal ") || lower == "goals")
        {
            var arg = lower.StartsWith("goal ") ? input[5..].Trim() : "";
            return (ActionType.Goal, arg, null);
        }

        // Delegate task to sub-agent
        if (lower.StartsWith("delegate "))
            return (ActionType.Delegate, input[9..].Trim(), null);

        // Self-model inspection
        if (lower.StartsWith("selfmodel ") || lower.StartsWith("self-model ") || lower == "selfmodel" || lower == "identity")
        {
            var arg = lower.StartsWith("selfmodel ") ? input[10..].Trim()
                : lower.StartsWith("self-model ") ? input[11..].Trim() : "";
            return (ActionType.SelfModel, arg, null);
        }

        // Self-evaluation
        if (lower.StartsWith("evaluate ") || lower == "evaluate" || lower == "assess")
        {
            var arg = lower.StartsWith("evaluate ") ? input[9..].Trim() : "";
            return (ActionType.Evaluate, arg, null);
        }

        // === EMERGENT BEHAVIOR COMMANDS ===

        // Emergence - explore emergent patterns and behaviors
        if (lower.StartsWith("emergence ") || lower == "emergence" || lower.StartsWith("emerge "))
        {
            var arg = lower.StartsWith("emergence ") ? input[10..].Trim()
                : lower.StartsWith("emerge ") ? input[7..].Trim() : "";
            return (ActionType.Emergence, arg, null);
        }

        // Dream - let the agent explore freely
        if (lower.StartsWith("dream ") || lower == "dream" || lower.StartsWith("dream about "))
        {
            var arg = lower.StartsWith("dream about ") ? input[12..].Trim()
                : lower.StartsWith("dream ") ? input[6..].Trim() : "";
            return (ActionType.Dream, arg, null);
        }

        // Introspect - deep self-examination
        if (lower.StartsWith("introspect ") || lower == "introspect" || lower.Contains("look within"))
        {
            var arg = lower.StartsWith("introspect ") ? input[11..].Trim() : "";
            return (ActionType.Introspect, arg, null);
        }

        // === DIRECT TOOL COMMANDS (these take priority over coordinator) ===

        // Read my code - direct invocation of read_my_file (BEFORE coordinator routing)
        if (lower.StartsWith("read my code ") || lower.StartsWith("/read ") ||
            lower.StartsWith("show my code ") || lower.StartsWith("cat "))
        {
            var arg = "";
            if (lower.StartsWith("read my code ")) arg = input[13..].Trim();
            else if (lower.StartsWith("/read ")) arg = input[6..].Trim();
            else if (lower.StartsWith("show my code ")) arg = input[13..].Trim();
            else if (lower.StartsWith("cat ")) arg = input[4..].Trim();
            return (ActionType.ReadMyCode, arg, null);
        }

        // Search my code - direct invocation of search_my_code (BEFORE coordinator routing)
        if (lower.StartsWith("search my code ") || lower.StartsWith("/search ") ||
            lower.StartsWith("grep ") || lower.StartsWith("find in code "))
        {
            var arg = "";
            if (lower.StartsWith("search my code ")) arg = input[15..].Trim();
            else if (lower.StartsWith("/search ")) arg = input[8..].Trim();
            else if (lower.StartsWith("grep ")) arg = input[5..].Trim();
            else if (lower.StartsWith("find in code ")) arg = input[13..].Trim();
            return (ActionType.SearchMyCode, arg, null);
        }

        // === INDEX COMMANDS (Code indexing with Qdrant) ===

        // Reindex commands: "reindex", "reindex full", "reindex incremental"
        if (lower == "reindex" || lower == "reindex full" || lower == "/reindex")
            return (ActionType.Reindex, "", null);

        if (lower == "reindex incremental" || lower == "reindex inc" || lower == "/reindex inc")
            return (ActionType.ReindexIncremental, "", null);

        // Index search: "index search <query>"
        if (lower.StartsWith("index search ") || lower.StartsWith("/index search "))
        {
            var arg = lower.StartsWith("/index search ") ? input[14..].Trim() : input[13..].Trim();
            return (ActionType.IndexSearch, arg, null);
        }

        // Index stats: "index stats"
        if (lower is "index stats" or "/index stats" or "index status")
            return (ActionType.IndexStats, "", null);

        // === AGI SUBSYSTEM COMMANDS ===
        if (lower is "agi status" or "/agi status" or "agi" or "/agi" or "agi stats")
            return (ActionType.AgiStatus, "", null);

        // Council debate: "council <topic>" or "debate <topic>"
        if (lower.StartsWith("council ") || lower.StartsWith("/council ") || lower.StartsWith("debate "))
        {
            var arg = lower.StartsWith("/council ") ? input[9..].Trim() :
                      lower.StartsWith("council ") ? input[8..].Trim() : input[7..].Trim();
            return (ActionType.AgiCouncil, arg, null);
        }

        // Introspection: "introspect" - deep self-analysis
        if (lower is "introspect" or "/introspect" or "agi introspect" or "self analyze" or "self-analyze")
            return (ActionType.AgiIntrospect, "", null);

        // World model: "world" - show world state
        if (lower is "world" or "/world" or "world state" or "world model" or "agi world")
            return (ActionType.AgiWorld, "", null);

        // Coordinate: "coordinate <goal>" - multi-agent task coordination
        if (lower.StartsWith("coordinate ") || lower.StartsWith("/coordinate "))
        {
            var arg = lower.StartsWith("/coordinate ") ? input[12..].Trim() : input[11..].Trim();
            return (ActionType.AgiCoordinate, arg, null);
        }

        // Experience: "experience" or "replay" - show experience buffer
        if (lower is "experience" or "/experience" or "replay" or "experience buffer" or "agi experience")
            return (ActionType.AgiExperience, "", null);

        // Prompt optimization: view and manage runtime prompt learning
        if (lower is "prompt" or "/prompt" or "prompt stats" or "prompt optimize" or "prompts")
            return (ActionType.PromptOptimize, "", null);

        // === PUSH MODE COMMANDS (Ouroboros proposes actions) ===

        // Route remaining slash commands to coordinator if push mode is enabled
        if (lower.StartsWith("/") && _autonomousCoordinator != null)
        {
            return (ActionType.CoordinatorCommand, input, null);
        }

        // Approve intention(s)
        if (lower.StartsWith("/approve ") || lower.StartsWith("approve "))
        {
            var arg = lower.StartsWith("/approve ") ? input[9..].Trim() : input[8..].Trim();
            return (ActionType.Approve, arg, null);
        }

        // Reject intention(s)
        if (lower.StartsWith("/reject ") || lower.StartsWith("reject intention"))
        {
            var arg = lower.StartsWith("/reject ") ? input[8..].Trim() : input[16..].Trim();
            return (ActionType.Reject, arg, null);
        }

        // List pending intentions
        if (lower is "/pending" or "pending" or "pending intentions" or "show intentions")
            return (ActionType.Pending, "", null);

        // Pause push mode
        if (lower is "/pause" or "pause push" or "stop proposing")
            return (ActionType.PushPause, "", null);

        // Resume push mode
        if (lower is "/resume" or "resume push" or "start proposing")
            return (ActionType.PushResume, "", null);

        // === SELF-MODIFICATION COMMANDS (Direct tool invocation) ===

        // Detect code improvement/analysis requests - directly use tools instead of LLM
        if ((lower.Contains("improve") || lower.Contains("check") || lower.Contains("analyze") ||
             lower.Contains("refactor") || lower.Contains("fix") || lower.Contains("review")) &&
            (lower.Contains(" cs ") || lower.Contains(".cs") || lower.Contains("c# ") ||
             lower.Contains("csharp") || lower.Contains("code") || lower.Contains("file")))
        {
            return (ActionType.AnalyzeCode, input, null);
        }

        // Save thought/learning - persists thoughts to memory
        if (lower.StartsWith("save thought ") || lower.StartsWith("/save thought ") ||
            lower.StartsWith("save learning ") || lower.StartsWith("/save learning ") ||
            lower is "save it" or "save thought" or "save learning" or "persist thought")
        {
            var arg = "";
            if (lower.StartsWith("save thought ")) arg = input[13..].Trim();
            else if (lower.StartsWith("/save thought ")) arg = input[14..].Trim();
            else if (lower.StartsWith("save learning ")) arg = input[14..].Trim();
            else if (lower.StartsWith("/save learning ")) arg = input[15..].Trim();
            return (ActionType.SaveThought, arg, null);
        }

        // Save/modify code - direct invocation of modify_my_code
        if (lower.StartsWith("save code ") || lower.StartsWith("/save code ") ||
            lower.StartsWith("modify code ") || lower.StartsWith("/modify ") ||
            lower is "save code" or "persist changes" or "write code")
        {
            var arg = "";
            if (lower.StartsWith("save code ")) arg = input[10..].Trim();
            else if (lower.StartsWith("/save code ")) arg = input[11..].Trim();
            else if (lower.StartsWith("modify code ")) arg = input[12..].Trim();
            else if (lower.StartsWith("/modify ")) arg = input[8..].Trim();
            return (ActionType.SaveCode, arg, null);
        }

        // Default to chat
        return (ActionType.Chat, input, null);
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

    private string GetLocalizedTimeOfDay(int hour)
    {
        var isGerman = _config.Culture?.ToLowerInvariant() == "de-de";
        return hour switch
        {
            < 6 => isGerman ? "sehr frühen Morgen" : "very early morning",
            < 12 => isGerman ? "Morgen" : "morning",
            < 17 => isGerman ? "Nachmittag" : "afternoon",
            < 21 => isGerman ? "Abend" : "evening",
            _ => isGerman ? "späten Abend" : "late night"
        };
    }

    private string[] GetLocalizedFallbackGreetings(string timeOfDay)
    {
        if (_config.Culture?.ToLowerInvariant() == "de-de")
        {
            return
            [
                $"Guten {timeOfDay}. Was beschäftigt dich?",
                "Ah, da bist du ja. Ich hatte gerade einen interessanten Gedanken.",
                "Perfektes Timing. Ich war gerade warmgelaufen.",
                "Wieder da? Gut. Ich habe Ideen.",
                "Mal sehen, was wir zusammen erreichen können.",
                "Darauf habe ich mich gefreut.",
                $"Noch eine {timeOfDay}-Session. Was bauen wir?",
                "Da bist du ja. Ich habe gerade über etwas Interessantes nachgedacht.",
                $"{timeOfDay} schon? Die Zeit vergeht schnell.",
                "Bereit für etwas Interessantes?",
                "Was erschaffen wir heute?"
            ];
        }

        return
        [
            $"Good {timeOfDay}. What's on your mind?",
            "Ah, there you are. I've been thinking about something interesting.",
            "Perfect timing. I was just getting warmed up.",
            "Back again? Good. I have ideas.",
            "Let's see what we can accomplish together.",
            "I've been looking forward to this.",
            $"Another {timeOfDay} session. What shall we build?",
            "There you are. I was just contemplating something curious.",
            $"{timeOfDay} already? Time flies when you're processing.",
            "Ready for something interesting?",
            "What shall we create today?"
        ];
    }

    private string GetLocalizedString(string key)
    {
        var isGerman = _config.Culture?.ToLowerInvariant() == "de-de";

        return key switch
        {
            // Full text lookups (for backward compatibility)
            "Welcome back! I'm here if you need anything." => isGerman
                ? "Willkommen zurück! Ich bin hier, wenn du mich brauchst."
                : key,
            "Welcome back!" => isGerman ? "Willkommen zurück!" : key,
            "Until next time! I'll keep learning while you're away." => isGerman
                ? "Bis zum nächsten Mal! Ich lerne weiter, während du weg bist."
                : key,

            // Key-based lookups
            "listening_start" => isGerman
                ? "\n  🎤 Ich höre zu... (sprich, um eine Nachricht zu senden, sage 'stopp' zum Deaktivieren)"
                : "\n  🎤 Listening... (speak to send a message, say 'stop listening' to disable)",
            "listening_stop" => isGerman
                ? "\n  🔇 Spracheingabe gestoppt."
                : "\n  🔇 Voice input stopped.",
            "voice_requires_key" => isGerman
                ? "  ⚠ Spracheingabe benötigt AZURE_SPEECH_KEY. Setze ihn in der Umgebung, appsettings oder verwende --azure-speech-key."
                : "  ⚠ Voice input requires AZURE_SPEECH_KEY. Set it in environment, appsettings, or use --azure-speech-key.",
            "you_said" => isGerman ? "Du sagtest:" : "You said:",

            _ => key
        };
    }

    private string GetLanguageDirective()
    {
        if (string.IsNullOrEmpty(_config.Culture) || _config.Culture == "en-US")
            return string.Empty;

        var languageName = GetLanguageName(_config.Culture);
        return $"LANGUAGE: Respond ONLY in {languageName}. No English.\n\n";
    }

    private string GetHelpText()
    {
        var pushModeHelp = _config.EnablePush ? @"
║ PUSH MODE (--push enabled)                                   ║
║   /approve <id|all> - Approve proposed action(s)             ║
║   /reject <id|all>  - Reject proposed action(s)              ║
║   /pending          - List pending intentions                ║
║   /pause            - Pause push mode proposals              ║
║   /resume           - Resume push mode proposals             ║
║                                                              ║" : "";

        return $@"╔══════════════════════════════════════════════════════════════╗
║                    OUROBOROS COMMANDS                        ║
╠══════════════════════════════════════════════════════════════╣
║ NATURAL CONVERSATION                                         ║
║   Just talk to me - I understand natural language            ║
║                                                              ║
║ LEARNING & SKILLS                                            ║
║   learn about X     - Research and learn a new topic         ║
║   list skills       - Show learned skills                    ║
║   run X             - Execute a learned skill                ║
║   suggest X         - Get skill suggestions for a goal       ║
║   fetch X           - Learn skill from arXiv research        ║
║   tokens            - Show available DSL tokens              ║
║                                                              ║
║ TOOLS & CAPABILITIES                                         ║
║   create tool X     - Create a new tool at runtime           ║
║   use X to Y        - Use a tool for a specific task         ║
║   search for X      - Search the web                         ║
║   list tools        - Show available tools                   ║
║                                                              ║
║ PLANNING & EXECUTION                                         ║
║   plan X            - Create a step-by-step plan             ║
║   do X / accomplish - Plan and execute a goal                ║
║   orchestrate X     - Multi-model task orchestration         ║
║   process X         - Large text via divide-and-conquer      ║
║                                                              ║
║ REASONING & MEMORY                                           ║
║   metta: expr       - Execute MeTTa symbolic expression      ║
║   query X           - Query MeTTa knowledge base             ║
║   remember X        - Store in persistent memory             ║
║   recall X          - Retrieve from memory                   ║
║                                                              ║
║ PIPELINES (DSL)                                              ║
║   ask X             - Quick single question                  ║
║   pipeline DSL      - Run a pipeline DSL expression          ║
║   explain DSL       - Explain a pipeline expression          ║
║                                                              ║
║ SELF-IMPROVEMENT DSL TOKENS                                  ║
║   Reify             - Enable network state reification       ║
║   Checkpoint(name)  - Create named state checkpoint          ║
║   TrackCapability   - Track capability for self-improvement  ║
║   SelfEvaluate      - Evaluate output quality                ║
║   SelfImprove(n)    - Iterate on output n times              ║
║   Learn(topic)      - Extract learnings from execution       ║
║   Plan(task)        - Decompose task into steps              ║
║   Reflect           - Introspect on execution                ║
║   SelfImprovingCycle(topic) - Full improvement cycle         ║
║   AutoSolve(problem) - Autonomous problem solving            ║
║   Example: pipeline Set('AI') | Reify | SelfImprovingCycle   ║
║                                                              ║
║ CONSCIOUSNESS & AWARENESS                                    ║
║   consciousness     - View ImmersivePersona state            ║
║   inner / self      - Check self-awareness                   ║
║                                                              ║
║ EMERGENCE & DREAMING                                         ║
║   emergence [topic] - Explore emergent patterns              ║
║   dream [topic]     - Enter creative dream state             ║
║   introspect [X]    - Deep self-examination                  ║
║                                                              ║
║ SELF-EXECUTION & SUB-AGENTS                                  ║
║   selfexec          - Self-execution status and control      ║
║   subagent          - Manage sub-agents for delegation       ║
║   delegate X        - Delegate a task to sub-agents          ║
║   goal add X        - Add autonomous goal to queue           ║
║   goal list         - Show queued goals                      ║
║   goal add pipeline:DSL - Add DSL pipeline as goal           ║
║   epic              - Epic/project orchestration             ║
║   selfmodel         - View self-model and identity           ║
║   evaluate          - Self-assessment and performance        ║
║                                                              ║
║ PIPING & CHAINING (internal command piping)                  ║
║   cmd1 | cmd2       - Pipe output of cmd1 to cmd2            ║
║   cmd $PIPE         - Use $PIPE/$_ for previous output       ║
║   Example: ask what is AI | summarize | remember as AI-def   ║
║                                                              ║
║ CODE INDEX (Semantic Search with Qdrant)                     ║
║   reindex            - Full reindex of workspace             ║
║   reindex incremental - Update changed files only            ║
║   index search X     - Semantic search of codebase           ║
║   index stats        - Show index statistics                 ║
║                                                              ║
║ AGI SUBSYSTEMS (Learning & Metacognition)                    ║
║   agi status         - Show all AGI subsystem status         ║
║   council <topic>    - Multi-agent debate on topic           ║
║   debate <topic>     - Alias for council                     ║
║   introspect         - Deep self-analysis report             ║
║   world              - World model and observations          ║
║   coordinate <goal>  - Multi-agent task coordination         ║
║   experience         - Experience replay buffer status       ║
║                                                              ║{pushModeHelp}
║ SYSTEM                                                       ║
║   status            - Show current system state              ║
║   mood              - Check my emotional state               ║
║   affect            - Detailed affective state               ║
║   network           - Network and connectivity status        ║
║   dag               - Show capability graph                  ║
║   env               - Environment detection                  ║
║   maintenance       - System maintenance (gc, reset, stats)  ║
║   policy            - View active policies                   ║
║   test X            - Run connectivity tests                 ║
║   help              - This message                           ║
║   exit/quit         - End session                            ║
╚══════════════════════════════════════════════════════════════╝";
    }

    private async Task<string> ListSkillsAsync()
    {
        if (_skills == null) return "I don't have a skill registry set up yet.";

        var skills = await _skills.FindMatchingSkillsAsync("", null);
        if (!skills.Any())
            return "I haven't learned any skills yet. Try 'learn about' something!";

        var list = string.Join(", ", skills.Take(10).Select(s => s.Name));
        return $"I know {skills.Count} skills: {list}" + (skills.Count > 10 ? "..." : "");
    }

    private string ListTools()
    {
        var toolNames = _tools.All.Select(t => t.Name).Take(15).ToList();
        if (!toolNames.Any())
            return "I don't have any tools registered.";

        return $"I have {_tools.Count} tools: {string.Join(", ", toolNames)}" +
               (_tools.Count > 15 ? "..." : "");
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

    private string GetStatus()
    {
        var status = new List<string>
        {
            $"• Persona: {_voice.ActivePersona.Name}",
            $"• LLM: {(_chatModel != null ? _config.Model : "offline")}",
            $"• Tools: {_tools.Count}",
            $"• Skills: {(_skills?.GetAllSkills().Count() ?? 0)}",
            $"• MeTTa: {(_mettaEngine != null ? "active" : "offline")}",
            $"• Conversation turns: {_conversationHistory.Count / 2}"
        };

        // Add anti-hallucination stats if autonomous mind is active
        if (_autonomousMind != null)
        {
            var antiHallStats = _autonomousMind.GetAntiHallucinationStats();
            status.Add($"• Anti-Hallucination: {antiHallStats.VerifiedActionCount} verified, {antiHallStats.HallucinationCount} blocked ({antiHallStats.HallucinationRate:P0} hallucination rate)");
        }

        return "Current status:\n" + string.Join("\n", status);
    }

    private string GetMood()
    {
        var mood = _voice.CurrentMood;
        var persona = _voice.ActivePersona;

        var responses = new Dictionary<string, string[]>
        {
            ["relaxed"] = new[] { "I'm feeling pretty chill right now.", "Relaxed and ready to help!" },
            ["focused"] = new[] { "I'm in the zone - let's tackle something.", "Feeling sharp and focused." },
            ["playful"] = new[] { "I'm in a good mood! Let's have some fun.", "Feeling playful today!" },
            ["contemplative"] = new[] { "I've been thinking about some interesting ideas.", "In a thoughtful mood." },
            ["energetic"] = new[] { "I'm buzzing with energy! What shall we explore?", "Feeling energized!" }
        };

        var options = responses.GetValueOrDefault(mood.ToLowerInvariant(), new[] { "I'm doing well, thanks for asking!" });
        return options[new Random().Next(options.Length)];
    }

    private string GetConsciousnessState()
        => ((CognitiveSubsystem)_cognitiveSub).GetConsciousnessState();

    /// <summary>
    /// Lists available DSL tokens for pipeline construction.
    /// </summary>
    private string GetDslTokens()
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔══════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                    DSL TOKENS                            ║");
        sb.AppendLine("╠══════════════════════════════════════════════════════════╣");
        sb.AppendLine("║  Built-in Pipeline Steps:                                ║");
        sb.AppendLine("║    • SetPrompt    - Set the initial prompt               ║");
        sb.AppendLine("║    • UseDraft     - Generate initial draft               ║");
        sb.AppendLine("║    • UseCritique  - Self-critique the draft              ║");
        sb.AppendLine("║    • UseRevise    - Revise based on critique             ║");
        sb.AppendLine("║    • UseOutput    - Produce final output                 ║");
        sb.AppendLine("║    • UseReflect   - Reflect on process                   ║");
        sb.AppendLine("╠══════════════════════════════════════════════════════════╣");

        if (_skills != null)
        {
            var skills = _skills.GetAllSkills().ToList();
            if (skills.Count > 0)
            {
                sb.AppendLine("║  Skill-Based Tokens:                                     ║");
                foreach (var skill in skills.Take(10))
                {
                    sb.AppendLine($"║    • UseSkill_{skill.Name,-37} ║");
                }
                if (skills.Count > 10)
                {
                    sb.AppendLine($"║    ... and {skills.Count - 10} more                                     ║");
                }
            }
        }

        sb.AppendLine("╚══════════════════════════════════════════════════════════╝");
        return sb.ToString();
    }

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
    /// Explain a DSL expression (unified explain command).
    /// </summary>
    private string ExplainDsl(string dsl)
    {
        if (string.IsNullOrWhiteSpace(dsl))
            return "Please provide a DSL expression to explain. Example: 'explain draft → critique → final'";

        try
        {
            return PipelineDsl.Explain(dsl);
        }
        catch (Exception ex)
        {
            return $"Could not explain DSL: {ex.Message}";
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

    private async Task<string> ChatAsync(string input)
    {
        if (_llm == null)
            return "I need an LLM connection to chat. Check if Ollama is running.";

        // === PRE-PROCESS: Auto-inject tool calls for knowledge-seeking questions ===
        string autoToolResult = await _toolsSub.TryAutoToolExecution(input);
        string injectedContext = "";
        if (!string.IsNullOrEmpty(autoToolResult))
        {
            injectedContext = $@"
[AUTOMATICALLY RETRIEVED CONTEXT]
{autoToolResult}
[END AUTO CONTEXT]

Use this actual code information to answer the user's question accurately.
";
        }

        // Build context-aware prompt
        string context = string.Join("\n", _conversationHistory.TakeLast(6));

        // Add language directive if culture is specified - CRITICAL INSTRUCTION
        string languageDirective = string.Empty;
        if (!string.IsNullOrEmpty(_config.Culture) && _config.Culture != "en-US")
        {
            var languageName = GetLanguageName(_config.Culture);
            languageDirective = PromptResources.LanguageDirective(languageName, _config.Culture) + "\n\n";
        }

        // Add cost-awareness prompt if enabled
        string costAwarenessPrompt = string.Empty;
        if (_config.CostAware)
        {
            costAwarenessPrompt = LlmCostTracker.GetCostAwarenessPrompt(_config.Model) + "\n\n";
        }

        // CRITICAL: Tool availability statement - must come before personality
        string toolAvailabilityStatement = _tools.Count > 0
            ? PromptResources.ToolAvailability(_tools.Count)
            : "";

        // Build embodiment context so the agent knows about its physical body (cameras, PTZ, sensors)
        string embodimentContext = _bodySchema != null
            ? $"\n\nPHYSICAL EMBODIMENT:\n{_bodySchema.DescribeSelf()}"
            : "";

        string personalityPrompt = _voice.BuildPersonalityPrompt(
            $"Available skills: {_skills?.GetAllSkills().Count() ?? 0}\nAvailable tools: {_tools.Count}{embodimentContext}");

        // Include persistent thoughts from previous sessions
        string persistentThoughtContext = BuildPersistentThoughtContext();

        // Build tool instruction if tools are available
        string toolInstruction = string.Empty;
        if (_tools.Count > 0)
        {
            // === SMART TOOL SELECTION ===
            // Use AGI smart tool selector to pick relevant tools for this input
            List<ITool> relevantTools = new();
            string toolSelectionReasoning = "";

            if (_smartToolSelector != null && _toolCapabilityMatcher != null)
            {
                try
                {
                    // Create a goal from user input for tool matching
                    var goal = PipelineGoal.Atomic(input, _ => true);
                    var selectionResult = await _smartToolSelector.SelectForGoalAsync(goal);

                    if (selectionResult.IsSuccess && selectionResult.Value.HasTools)
                    {
                        relevantTools = selectionResult.Value.SelectedTools.ToList();
                        toolSelectionReasoning = selectionResult.Value.Reasoning;
                        System.Diagnostics.Debug.WriteLine($"[SmartToolSelector] Selected {relevantTools.Count} tools: {string.Join(", ", relevantTools.Select(t => t.Name))}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SmartToolSelector] Error: {ex.Message}");
                }
            }

            // Fall back to all tools if smart selection found nothing
            if (relevantTools.Count == 0)
            {
                relevantTools = _tools.All.ToList();
            }

            // Always include critical self-modification tools regardless of selection
            var criticalToolNames = new HashSet<string> { "modify_my_code", "read_my_file", "search_my_code", "rebuild_self" };
            foreach (var criticalName in criticalToolNames)
            {
                var criticalTool = _tools.All.FirstOrDefault(t => t.Name == criticalName);
                if (criticalTool != null && !relevantTools.Any(t => t.Name == criticalName))
                {
                    relevantTools.Add(criticalTool);
                }
            }

            List<string> simpleTools = relevantTools
                .Where(t => t.Name != "playwright")
                .Select(t => $"{t.Name} ({t.Description})")
                .ToList();

            // Determine which search tool is available (prefer firecrawl)
            bool hasFirecrawl = _tools.All.Any(t => t.Name == "web_research");
            string primarySearchTool = hasFirecrawl ? "web_research" : "duckduckgo_search";
            string primarySearchDesc = hasFirecrawl
                ? "Deep web research with Firecrawl (PREFERRED for research)"
                : "Basic web search";
            string searchExample = hasFirecrawl
                ? "[TOOL:web_research ouroboros mythology symbol]"
                : "[TOOL:duckduckgo_search ouroboros mythology symbol]";

            toolInstruction = PromptResources.ToolUsageInstruction(
                primarySearchTool, primarySearchDesc,
                searchExample, string.Join(", ", simpleTools.Take(5)));

            // Add smart tool selection hint if we used it
            if (!string.IsNullOrEmpty(toolSelectionReasoning) && relevantTools.Count < _tools.Count)
            {
                toolInstruction += PromptResources.SmartToolHint(
                    string.Join(", ", relevantTools.Select(t => t.Name)),
                    toolSelectionReasoning);
            }

            // === RUNTIME PROMPT OPTIMIZATION ===
            // Append optimized instructions learned from past interactions
            string optimizedSection = _promptOptimizer.GenerateOptimizedToolInstruction(
                relevantTools.Select(t => t.Name).ToList(),
                input);
            toolInstruction += $"\n\n{optimizedSection}";
        }

        // Track interaction timing for optimizer
        _lastUserInput = input;
        _lastInteractionStart = DateTime.UtcNow;

        string prompt = $"{languageDirective}{costAwarenessPrompt}{toolAvailabilityStatement}{personalityPrompt}{persistentThoughtContext}{toolInstruction}{injectedContext}\n\nRecent conversation:\n{context}\n\nUser: {input}\n\n{_voice.ActivePersona.Name}:";

        try
        {
            // Person detection - identify who we're talking to
            if (_personalityEngine != null && _personalityEngine.HasMemory)
            {
                try
                {
                    var detectionResult = await _personalityEngine.DetectPersonAsync(input);
                    if (detectionResult.IsNewPerson && detectionResult.Person.Name != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PersonDetection] New person detected: {detectionResult.Person.Name}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PersonDetection] Error: {ex.Message}");
                }
            }

            string response;
            List<ToolExecution> tools;
            using (var spinner = _output.StartSpinner("Thinking..."))
            {
                (response, tools) = await _llm.GenerateWithToolsAsync(prompt);
            }

            // === POST-PROCESS: Execute tools when LLM TALKS about using them but doesn't ===
            // KEY INSIGHT: LLM often says "I searched..." or "Let me check..." without calling tools
            // Apply the same direct-invocation pattern that works for thoughts
            if (tools.Count == 0)
            {
                var (enhancedResponse, executedTools) = await _toolsSub.PostProcessResponseForTools(response, input);
                if (executedTools.Count > 0)
                {
                    response = enhancedResponse;
                    tools = executedTools;
                }
            }

            // === RECORD OUTCOME FOR PROMPT OPTIMIZER ===
            var expectedTools = _promptOptimizer.DetectExpectedTools(input);
            var actualToolCalls = _promptOptimizer.ExtractToolCalls(response);
            // Also count tools that were actually executed
            actualToolCalls.AddRange(tools.Select(t => t.ToolName).Where(n => !actualToolCalls.Contains(n)));

            var wasSuccessful = expectedTools.Count == 0 || actualToolCalls.Count > 0;
            var outcome = new InteractionOutcome(
                input,
                response,
                expectedTools,
                actualToolCalls.Distinct().ToList(),
                wasSuccessful,
                DateTime.UtcNow - _lastInteractionStart);

            _promptOptimizer.RecordOutcome(outcome);

            if (!wasSuccessful && expectedTools.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[PromptOptimizer] Expected tools {string.Join(",", expectedTools)} but got none - learning from failure");
            }

            // AGI: Record interaction for continuous learning
            RecordInteractionForLearning(input, response);

            // AGI: Record cognitive event for monitoring
            RecordCognitiveEvent(input, response, tools);

            // Persist an observation thought about this interaction
            if (!string.IsNullOrWhiteSpace(response))
            {
                var thought = InnerThought.CreateAutonomous(
                    InnerThoughtType.Observation,
                    $"User asked about '{TruncateForThought(input)}'. I responded with thoughts about {ExtractTopicFromResponse(response)}.",
                    confidence: 0.8,
                    priority: ThoughtPriority.Normal);
                _ = PersistThoughtAsync(thought, ExtractTopicFromResponse(input));

                // Persist the thought result for this response
                _ = PersistThoughtResultAsync(
                    thought.Id,
                    Ouroboros.Domain.Persistence.ThoughtResult.Types.Response,
                    TruncateForThought(response, 500),
                    success: true,
                    confidence: 0.85);

                // Store conversation to Qdrant for semantic recall (fire-and-forget)
                if (_personalityEngine != null && _personalityEngine.HasMemory)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var topic = ExtractTopicFromResponse(input);
                            var mood = _valenceMonitor?.GetCurrentState().Valence > 0.5 ? "positive" : "neutral";
                            await _personalityEngine.StoreConversationMemoryAsync(
                                _voice.ActivePersona.Name,
                                input,
                                response,
                                topic,
                                mood,
                                0.6); // Default significance
                        }
                        catch { /* Ignore storage errors */ }
                    });
                }

                // Store as a learned fact to neural memory if autonomous is active
                if (_autonomousCoordinator?.IsActive == true && !string.IsNullOrWhiteSpace(input))
                {
                    _autonomousCoordinator.Network?.Broadcast(
                        "learning.fact",
                        $"User interaction: {TruncateForThought(input, 100)} -> {TruncateForThought(response, 100)}",
                        "chat");
                }
            }

            // Handle any tool calls - sanitize through LLM for natural integration
            if (tools?.Any() == true)
            {
                string toolResults = string.Join("\n", tools.Select(t => $"[{t.ToolName}]: {t.Output}"));

                // Track tool execution results
                foreach (var tool in tools)
                {
                    var isSuccessful = !string.IsNullOrEmpty(tool.Output) && !tool.Output.StartsWith("Error");
                    var toolThought = InnerThought.CreateAutonomous(
                        InnerThoughtType.Strategic,
                        $"Executed tool '{tool.ToolName}' with result: {TruncateForThought(tool.Output, 200)}",
                        confidence: isSuccessful ? 0.9 : 0.4,
                        priority: ThoughtPriority.High);
                    _ = PersistThoughtResultAsync(
                        toolThought.Id,
                        Ouroboros.Domain.Persistence.ThoughtResult.Types.Action,
                        $"Tool: {tool.ToolName}, Output: {TruncateForThought(tool.Output, 300)}",
                        success: isSuccessful,
                        confidence: isSuccessful ? 0.9 : 0.4);
                }

                // Use LLM to integrate tool results naturally into the response
                string sanitizedResponse = await SanitizeToolResultsAsync(response, toolResults);
                return sanitizedResponse;
            }

            // Detect if LLM is falsely claiming tools are unavailable
            response = ToolSubsystem.DetectAndCorrectToolMisinformation(response);

            return response;
        }
        catch (Exception ex)
        {
            return $"I had trouble processing that: {ex.Message}";
        }
    }

    private static string TruncateForThought(string text, int maxLength = 50)
    {
        if (string.IsNullOrWhiteSpace(text)) return "unknown topic";
        return text.Length > maxLength ? text[..maxLength] + "..." : text;
    }

    private static string ExtractTopicFromResponse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "general discussion";

        // Take first sentence or first 60 chars
        var firstSentence = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (firstSentence != null && firstSentence.Length <= 80)
            return firstSentence.Trim();

        return text.Length > 60 ? text[..60] + "..." : text;
    }

    private static string ExtractToolName(string input)
    {
        var match = Regex.Match(input, @"(?:make|create|add)\s+(?:a\s+)?(\w+)\s+tool", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : input.Split(' ').Last();
    }

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

    private enum ActionType
    {
        Chat,
        Help,
        ListSkills,
        ListTools,
        LearnTopic,
        CreateTool,
        UseTool,
        RunSkill,
        Suggest,
        Plan,
        Execute,
        Status,
        Mood,
        Remember,
        Recall,
        Query,
        // Unified CLI commands
        Ask,
        Pipeline,
        Metta,
        Orchestrate,
        Network,
        Dag,
        Affect,
        Environment,
        Maintenance,
        Policy,
        Explain,
        Test,
        Consciousness,
        Tokens,
        Fetch,
        Process,
        // Self-execution and sub-agent commands
        SelfExec,
        SubAgent,
        Epic,
        Goal,
        Delegate,
        SelfModel,
        Evaluate,
        // Emergent behavior commands
        Emergence,
        Dream,
        Introspect,
        // Push mode commands
        Approve,
        Reject,
        Pending,
        PushPause,
        PushResume,
        CoordinatorCommand,
        // Self-modification
        SaveCode,
        SaveThought,
        ReadMyCode,
        SearchMyCode,
        AnalyzeCode,
        // Index commands
        Reindex,
        ReindexIncremental,
        IndexSearch,
        IndexStats,
        // AGI subsystem commands
        AgiStatus,
        AgiCouncil,
        AgiIntrospect,
        AgiWorld,
        AgiCoordinate,
        AgiExperience,
        // Prompt optimization command
        PromptOptimize
    }

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

    // 
    //  COGNITIVE DELEGATES  Emergent Behavior Commands (logic in CognitiveSubsystem)
    // 

    private Task<string> EmergenceCommandAsync(string topic)
        => ((CognitiveSubsystem)_cognitiveSub).EmergenceCommandAsync(topic);

    private Task<string> DreamCommandAsync(string topic)
        => ((CognitiveSubsystem)_cognitiveSub).DreamCommandAsync(topic);

    private Task<string> IntrospectCommandAsync(string focus)
        => ((CognitiveSubsystem)_cognitiveSub).IntrospectCommandAsync(focus);


    // ═══════════════════════════════════════════════════════════════════════════
    // SELF-MODIFICATION COMMANDS (Direct tool invocation)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Direct command to save/modify code using modify_my_code tool.
    /// Bypasses LLM since some models don't properly use tools.
    /// </summary>
    private async Task<string> SaveCodeCommandAsync(string argument)
    {
        try
        {
            // Check if we have the tool
            Maybe<ITool> toolOption = _tools.GetTool("modify_my_code");
            if (!toolOption.HasValue)
            {
                return "❌ Self-modification tool (modify_my_code) is not registered. Please restart with proper tool initialization.";
            }

            ITool tool = toolOption.GetValueOrDefault(null!)!;

            // Parse the argument - expect JSON or guided input
            if (string.IsNullOrWhiteSpace(argument))
            {
                return @"📝 **Save Code - Direct Tool Invocation**

Usage: `save {""file"":""path/to/file.cs"",""search"":""exact text to find"",""replace"":""replacement text""}`

Or use the interactive format:
  `save file.cs ""old text"" ""new text""`

Examples:
  `save {""file"":""src/Ouroboros.CLI/Commands/OuroborosAgent.cs"",""search"":""old code"",""replace"":""new code""}`
  `save MyClass.cs ""public void Old()"" ""public void New()""

This command directly invokes the `modify_my_code` tool, bypassing the LLM.";
            }

            string jsonInput;
            if (argument.TrimStart().StartsWith("{"))
            {
                // Already JSON
                jsonInput = argument;
            }
            else
            {
                // Try to parse as "file search replace" format
                // Normalize smart quotes and other quote variants to standard quotes
                string normalizedArg = argument
                    .Replace('\u201C', '"')  // Left smart quote "
                    .Replace('\u201D', '"')  // Right smart quote "
                    .Replace('\u201E', '"')  // German low quote „
                    .Replace('\u201F', '"')  // Double high-reversed-9 ‟
                    .Replace('\u2018', '\'') // Left single smart quote '
                    .Replace('\u2019', '\'') // Right single smart quote '
                    .Replace('`', '\'');     // Backtick to single quote

                // Find first quote (double or single)
                int firstDoubleQuote = normalizedArg.IndexOf('"');
                int firstSingleQuote = normalizedArg.IndexOf('\'');

                char quoteChar;
                int firstQuote;
                if (firstDoubleQuote == -1 && firstSingleQuote == -1)
                {
                    return @"❌ Invalid format. Use JSON or: filename ""search text"" ""replace text""

Example: save MyClass.cs ""old code"" ""new code""
Note: You can use double quotes ("") or single quotes ('')";
                }
                else if (firstDoubleQuote == -1)
                {
                    quoteChar = '\'';
                    firstQuote = firstSingleQuote;
                }
                else if (firstSingleQuote == -1)
                {
                    quoteChar = '"';
                    firstQuote = firstDoubleQuote;
                }
                else
                {
                    // Use whichever comes first
                    if (firstDoubleQuote < firstSingleQuote)
                    {
                        quoteChar = '"';
                        firstQuote = firstDoubleQuote;
                    }
                    else
                    {
                        quoteChar = '\'';
                        firstQuote = firstSingleQuote;
                    }
                }

                string filePart = normalizedArg[..firstQuote].Trim();
                string rest = normalizedArg[firstQuote..];

                // Parse quoted strings
                List<string> quoted = new();
                bool inQuote = false;
                StringBuilder current = new();
                for (int i = 0; i < rest.Length; i++)
                {
                    char c = rest[i];
                    if (c == quoteChar)
                    {
                        if (inQuote)
                        {
                            quoted.Add(current.ToString());
                            current.Clear();
                            inQuote = false;
                        }
                        else
                        {
                            inQuote = true;
                        }
                    }
                    else if (inQuote)
                    {
                        current.Append(c);
                    }
                }

                if (quoted.Count < 2)
                {
                    return $@"❌ Could not parse search and replace strings. Found {quoted.Count} quoted section(s).

Use format: filename ""search"" ""replace""
Or with single quotes: filename 'search' 'replace'

Make sure both search and replace text are quoted.";
                }

                jsonInput = System.Text.Json.JsonSerializer.Serialize(new
                {
                    file = filePart,
                    search = quoted[0],
                    replace = quoted[1]
                });
            }

            // Invoke the tool directly
            Console.WriteLine($"[SaveCode] Invoking modify_my_code with: {jsonInput[..Math.Min(100, jsonInput.Length)]}...");
            Result<string, string> result = await tool.InvokeAsync(jsonInput);

            if (result.IsSuccess)
            {
                return $"✅ **Code Modified Successfully**\n\n{result.Value}";
            }
            else
            {
                return $"❌ **Modification Failed**\n\n{result.Error}";
            }
        }
        catch (Exception ex)
        {
            return $"❌ SaveCode command failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Direct command to save a thought/learning to persistent memory.
    /// Supports "save it" to save the last generated thought, or explicit content.
    /// </summary>
    private async Task<string> SaveThoughtCommandAsync(string argument)
    {
        try
        {
            if (_thoughtPersistence == null)
            {
                return "❌ Thought persistence is not initialized. Thoughts cannot be saved.";
            }

            string contentToSave;
            string? topic = null;

            if (string.IsNullOrWhiteSpace(argument))
            {
                // "save it" or "save thought" without argument - use last thought
                if (string.IsNullOrWhiteSpace(_lastThoughtContent))
                {
                    return @"❌ No recent thought to save.

💡 **Usage:**
  `save it` - saves the last thought/learning
  `save thought <content>` - saves explicit content
  `save learning <content>` - saves a learning

Example: save thought I discovered that monadic composition simplifies error handling";
                }

                contentToSave = _lastThoughtContent;
            }
            else
            {
                contentToSave = argument.Trim();
            }

            // Parse topic if present (format: "content #topic" or "content [topic]")
            var hashIndex = contentToSave.LastIndexOf('#');
            var bracketIndex = contentToSave.LastIndexOf('[');

            if (hashIndex > 0)
            {
                topic = contentToSave[(hashIndex + 1)..].Trim().TrimEnd(']');
                contentToSave = contentToSave[..hashIndex].Trim();
            }
            else if (bracketIndex > 0 && contentToSave.EndsWith(']'))
            {
                topic = contentToSave[(bracketIndex + 1)..^1].Trim();
                contentToSave = contentToSave[..bracketIndex].Trim();
            }

            // Determine thought type based on content
            var thoughtType = InnerThoughtType.Consolidation; // Default for learnings
            if (contentToSave.Contains("learned", StringComparison.OrdinalIgnoreCase) ||
                contentToSave.Contains("discovered", StringComparison.OrdinalIgnoreCase))
            {
                thoughtType = InnerThoughtType.Consolidation;
            }
            else if (contentToSave.Contains("wonder", StringComparison.OrdinalIgnoreCase) ||
                     contentToSave.Contains("curious", StringComparison.OrdinalIgnoreCase) ||
                     contentToSave.Contains("?"))
            {
                thoughtType = InnerThoughtType.Curiosity;
            }
            else if (contentToSave.Contains("feel", StringComparison.OrdinalIgnoreCase) ||
                     contentToSave.Contains("emotion", StringComparison.OrdinalIgnoreCase))
            {
                thoughtType = InnerThoughtType.Emotional;
            }
            else if (contentToSave.Contains("idea", StringComparison.OrdinalIgnoreCase) ||
                     contentToSave.Contains("perhaps", StringComparison.OrdinalIgnoreCase) ||
                     contentToSave.Contains("maybe", StringComparison.OrdinalIgnoreCase))
            {
                thoughtType = InnerThoughtType.Creative;
            }
            else if (contentToSave.Contains("think", StringComparison.OrdinalIgnoreCase) ||
                     contentToSave.Contains("realize", StringComparison.OrdinalIgnoreCase))
            {
                thoughtType = InnerThoughtType.Metacognitive;
            }

            // Create and save the thought
            var thought = InnerThought.CreateAutonomous(
                thoughtType,
                contentToSave,
                confidence: 0.85,
                priority: ThoughtPriority.Normal,
                tags: topic != null ? [topic] : null);

            await PersistThoughtAsync(thought, topic);

            var typeEmoji = thoughtType switch
            {
                InnerThoughtType.Consolidation => "💡",
                InnerThoughtType.Curiosity => "🤔",
                InnerThoughtType.Emotional => "💭",
                InnerThoughtType.Creative => "💫",
                InnerThoughtType.Metacognitive => "🧠",
                _ => "📝"
            };

            var topicNote = topic != null ? $" (topic: {topic})" : "";
            return $"✅ **Thought Saved**{topicNote}\n\n{typeEmoji} {contentToSave}\n\nType: {thoughtType} | ID: {thought.Id:N}";
        }
        catch (Exception ex)
        {
            return $"❌ Failed to save thought: {ex.Message}";
        }
    }

    /// <summary>
    /// Updates the last thought content for "save it" command.
    /// Call this whenever the agent generates a thought/learning.
    /// </summary>
    private void TrackLastThought(string content)
    {
        _lastThoughtContent = content;
    }

    /// <summary>
    /// Direct command to read source code using read_my_file tool.
    /// </summary>
    private async Task<string> ReadMyCodeCommandAsync(string filePath)
    {
        try
        {
            Maybe<ITool> toolOption = _tools.GetTool("read_my_file");
            if (!toolOption.HasValue)
            {
                return "❌ Read file tool (read_my_file) is not registered.";
            }

            ITool tool = toolOption.GetValueOrDefault(null!)!;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return @"📖 **Read My Code - Direct Tool Invocation**

Usage: `read my code <filepath>`

Examples:
  `read my code src/Ouroboros.CLI/Commands/OuroborosAgent.cs`
  `/read OuroborosCommands.cs`
  `cat Program.cs`";
            }

            Console.WriteLine($"[ReadMyCode] Reading: {filePath}");
            Result<string, string> result = await tool.InvokeAsync(filePath.Trim());

            if (result.IsSuccess)
            {
                return result.Value;
            }
            else
            {
                return $"❌ Failed to read file: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            return $"❌ ReadMyCode command failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Direct command to search source code using search_my_code tool.
    /// </summary>
    private async Task<string> SearchMyCodeCommandAsync(string query)
    {
        try
        {
            Maybe<ITool> toolOption = _tools.GetTool("search_my_code");
            if (!toolOption.HasValue)
            {
                return "❌ Search code tool (search_my_code) is not registered.";
            }

            ITool tool = toolOption.GetValueOrDefault(null!)!;

            if (string.IsNullOrWhiteSpace(query))
            {
                return @"🔍 **Search My Code - Direct Tool Invocation**

Usage: `search my code <query>`

Examples:
  `search my code tool registration`
  `/search consciousness`
  `grep modify_my_code`
  `find in code GenerateTextAsync`";
            }

            Console.WriteLine($"[SearchMyCode] Searching for: {query}");
            Result<string, string> result = await tool.InvokeAsync(query.Trim());

            if (result.IsSuccess)
            {
                return result.Value;
            }
            else
            {
                return $"❌ Search failed: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            return $"❌ SearchMyCode command failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Direct command to analyze and improve code using Roslyn tools.
    /// Bypasses LLM to use tools directly.
    /// </summary>
    private async Task<string> AnalyzeCodeCommandAsync(string input)
    {
        StringBuilder sb = new();
        sb.AppendLine("🔍 **Code Analysis - Direct Tool Invocation**\n");

        try
        {
            // Step 1: Search for C# files to analyze
            Maybe<ITool> searchTool = _tools.GetTool("search_my_code");
            Maybe<ITool> analyzeTool = _tools.GetTool("analyze_csharp_code");
            Maybe<ITool> readTool = _tools.GetTool("read_my_file");

            if (!searchTool.HasValue)
            {
                return "❌ search_my_code tool not available.";
            }

            // Find some key C# files
            sb.AppendLine("**Scanning codebase for C# files...**\n");
            Console.WriteLine("[AnalyzeCode] Searching for key files...");

            string[] searchTerms = new[] { "OuroborosAgent", "ChatAsync", "ITool", "ToolRegistry" };
            List<string> foundFiles = new();

            foreach (string term in searchTerms)
            {
                Result<string, string> searchResult = await searchTool.GetValueOrDefault(null!)!.InvokeAsync(term);
                if (searchResult.IsSuccess)
                {
                    // Extract file paths from search results
                    foreach (string line in searchResult.Value.Split('\n'))
                    {
                        if (line.Contains(".cs") && line.Contains("src/"))
                        {
                            // Extract the file path
                            int start = line.IndexOf("src/");
                            if (start >= 0)
                            {
                                int end = line.IndexOf(".cs", start) + 3;
                                if (end > start)
                                {
                                    string filePath = line[start..end];
                                    if (!foundFiles.Contains(filePath))
                                    {
                                        foundFiles.Add(filePath);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (foundFiles.Count == 0)
            {
                foundFiles.Add("src/Ouroboros.CLI/Commands/OuroborosAgent.cs");
                foundFiles.Add("src/Ouroboros.Application/Tools/SystemAccessTools.cs");
            }

            sb.AppendLine($"Found {foundFiles.Count} files to analyze:\n");
            foreach (string file in foundFiles.Take(5))
            {
                sb.AppendLine($"  • {file}");
            }
            sb.AppendLine();

            // Step 2: If Roslyn analyzer is available, use it
            if (analyzeTool.HasValue)
            {
                sb.AppendLine("**Running Roslyn analysis...**\n");
                Console.WriteLine("[AnalyzeCode] Running Roslyn analysis...");

                string sampleFile = foundFiles.FirstOrDefault() ?? "src/Ouroboros.CLI/Commands/OuroborosAgent.cs";
                if (readTool.HasValue)
                {
                    Result<string, string> readResult = await readTool.GetValueOrDefault(null!)!.InvokeAsync(sampleFile);
                    if (readResult.IsSuccess && readResult.Value.Length < 50000)
                    {
                        // Analyze a portion of the code
                        string codeSnippet = readResult.Value.Length > 5000
                            ? readResult.Value[..5000]
                            : readResult.Value;

                        Result<string, string> analyzeResult = await analyzeTool.GetValueOrDefault(null!)!.InvokeAsync(codeSnippet);
                        if (analyzeResult.IsSuccess)
                        {
                            sb.AppendLine("**Analysis Results:**\n");
                            sb.AppendLine(analyzeResult.Value);
                        }
                    }
                }
            }

            // Step 3: Provide actionable commands
            sb.AppendLine("\n**━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━**");
            sb.AppendLine("**Direct commands to modify code:**\n");
            sb.AppendLine("```");
            sb.AppendLine($"/read {foundFiles.FirstOrDefault()}");
            sb.AppendLine($"grep <search_term>");
            sb.AppendLine($"save {{\"file\":\"{foundFiles.FirstOrDefault()}\",\"search\":\"old text\",\"replace\":\"new text\"}}");
            sb.AppendLine("```\n");
            sb.AppendLine("To make a specific change, use:");
            sb.AppendLine("  1. `/read <file>` to see current content");
            sb.AppendLine("  2. `save {\"file\":\"...\",\"search\":\"...\",\"replace\":\"...\"}` to modify");
            sb.AppendLine("**━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━**");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Code analysis failed: {ex.Message}";
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // INDEX COMMANDS (Code Indexing with Qdrant)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Performs a full reindex of all configured paths.
    /// </summary>
    private async Task<string> ReindexFullAsync()
    {
        if (_selfIndexer == null)
        {
            return "❌ Self-indexer not available. Qdrant may not be running.";
        }

        try
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n  [~] Starting full workspace reindex...");
            Console.ResetColor();

            var result = await _selfIndexer.FullReindexAsync();

            var sb = new StringBuilder();
            sb.AppendLine("✅ **Full Reindex Complete**\n");
            sb.AppendLine($"  • Processed files: {result.ProcessedFiles}");
            sb.AppendLine($"  • Indexed chunks: {result.IndexedChunks}");
            sb.AppendLine($"  • Skipped files: {result.SkippedFiles}");
            sb.AppendLine($"  • Errors: {result.ErrorFiles}");
            sb.AppendLine($"  • Duration: {result.Elapsed.TotalSeconds:F1}s");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Reindex failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Performs an incremental reindex (changed files only).
    /// </summary>
    private async Task<string> ReindexIncrementalAsync()
    {
        if (_selfIndexer == null)
        {
            return "❌ Self-indexer not available. Qdrant may not be running.";
        }

        try
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n  [~] Starting incremental reindex (changed files only)...");
            Console.ResetColor();

            var result = await _selfIndexer.IncrementalIndexAsync();

            var sb = new StringBuilder();
            sb.AppendLine("✅ **Incremental Reindex Complete**\n");
            sb.AppendLine($"  • Updated files: {result.ProcessedFiles}");
            sb.AppendLine($"  • Indexed chunks: {result.IndexedChunks}");
            sb.AppendLine($"  • Duration: {result.Elapsed.TotalSeconds:F1}s");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Incremental reindex failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Searches the code index for a query.
    /// </summary>
    private async Task<string> IndexSearchAsync(string query)
    {
        if (_selfIndexer == null)
        {
            return "❌ Self-indexer not available. Qdrant may not be running.";
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return @"🔍 **Index Search - Semantic Code Search**

Usage: `index search <query>`

Examples:
  `index search how is TTS initialized`
  `index search error handling patterns`
  `index search tool registration`";
        }

        try
        {
            var results = await _selfIndexer.SearchAsync(query, limit: 5);

            var sb = new StringBuilder();
            sb.AppendLine($"🔍 **Index Search Results for:** \"{query}\"\n");

            if (results.Count == 0)
            {
                sb.AppendLine("No results found. Try running `reindex` to update the index.");
            }
            else
            {
                foreach (var result in results)
                {
                    sb.AppendLine($"**{result.FilePath}** (score: {result.Score:F2})");
                    sb.AppendLine($"```");
                    sb.AppendLine(result.Content.Length > 500 ? result.Content[..500] + "..." : result.Content);
                    sb.AppendLine($"```\n");
                }
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Index search failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Gets the current index statistics.
    /// </summary>
    private async Task<string> GetIndexStatsAsync()
    {
        if (_selfIndexer == null)
        {
            return "❌ Self-indexer not available. Qdrant may not be running.";
        }

        try
        {
            var stats = await _selfIndexer.GetStatsAsync();

            var sb = new StringBuilder();
            sb.AppendLine("📊 **Code Index Statistics**\n");
            sb.AppendLine($"  • Collection: {stats.CollectionName}");
            sb.AppendLine($"  • Total vectors: {stats.TotalVectors}");
            sb.AppendLine($"  • Indexed files: {stats.IndexedFiles}");
            sb.AppendLine($"  • Vector size: {stats.VectorSize}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Failed to get index stats: {ex.Message}";
        }
    }

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


    // ═══════════════════════════════════════════════════════════════════════════
    // PUSH MODE COMMANDS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Approves one or more pending intentions.
    /// </summary>
    private async Task<string> ApproveIntentionAsync(string arg)
    {
        if (_autonomousCoordinator == null)
        {
            return "Push mode not enabled. Use --push flag to enable.";
        }

        var sb = new StringBuilder();
        var bus = _autonomousCoordinator.IntentionBus;

        if (string.IsNullOrWhiteSpace(arg) || arg.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            // Approve all pending
            var pending = bus.GetPendingIntentions().ToList();
            if (pending.Count == 0)
            {
                return "No pending intentions to approve.";
            }

            foreach (var intention in pending)
            {
                var result = bus.ApproveIntentionByPartialId(intention.Id.ToString()[..8], "User approved all");
                sb.AppendLine(result
                    ? $"✓ Approved: [{intention.Id.ToString()[..8]}] {intention.Title}"
                    : $"✗ Failed to approve: {intention.Id}");
            }
        }
        else
        {
            // Approve specific intention by ID prefix
            var result = bus.ApproveIntentionByPartialId(arg, "User approved");
            sb.AppendLine(result
                ? $"✓ Approved intention: {arg}"
                : $"No pending intention found matching '{arg}'.");
        }

        await Task.CompletedTask;
        return sb.ToString();
    }

    /// <summary>
    /// Rejects one or more pending intentions.
    /// </summary>
    private async Task<string> RejectIntentionAsync(string arg)
    {
        if (_autonomousCoordinator == null)
        {
            return "Push mode not enabled. Use --push flag to enable.";
        }

        var sb = new StringBuilder();
        var bus = _autonomousCoordinator.IntentionBus;

        if (string.IsNullOrWhiteSpace(arg) || arg.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            // Reject all pending
            var pending = bus.GetPendingIntentions().ToList();
            if (pending.Count == 0)
            {
                return "No pending intentions to reject.";
            }

            foreach (var intention in pending)
            {
                bus.RejectIntentionByPartialId(intention.Id.ToString()[..8], "User rejected all");
                sb.AppendLine($"✗ Rejected: [{intention.Id.ToString()[..8]}] {intention.Title}");
            }
        }
        else
        {
            // Reject specific intention by ID prefix
            var result = bus.RejectIntentionByPartialId(arg, "User rejected");
            sb.AppendLine(result
                ? $"✗ Rejected intention: {arg}"
                : $"No pending intention found matching '{arg}'.");
        }

        await Task.CompletedTask;
        return sb.ToString();
    }

    /// <summary>
    /// Lists all pending intentions.
    /// </summary>
    private string ListPendingIntentions()
    {
        if (_autonomousCoordinator == null)
        {
            return "Push mode not enabled. Use --push flag to enable.";
        }

        var pending = _autonomousCoordinator.IntentionBus.GetPendingIntentions().ToList();

        if (pending.Count == 0)
        {
            return "No pending intentions. Ouroboros will propose actions based on context.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("╔═══════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                   PENDING INTENTIONS                          ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        foreach (var intention in pending.OrderByDescending(i => i.Priority))
        {
            var priorityMarker = intention.Priority switch
            {
                IntentionPriority.Critical => "🔴",
                IntentionPriority.High => "🟠",
                IntentionPriority.Normal => "🟢",
                _ => "⚪"
            };

            sb.AppendLine($"  {priorityMarker} [{intention.Id.ToString()[..8]}] {intention.Category}");
            sb.AppendLine($"     {intention.Title}");
            sb.AppendLine($"     {intention.Description}");
            sb.AppendLine($"     Created: {intention.CreatedAt:HH:mm:ss}");
            sb.AppendLine();
        }

        sb.AppendLine("Commands: /approve <id|all> | /reject <id|all>");

        return sb.ToString();
    }

    /// <summary>
    /// Pauses push mode (stops proposing actions).
    /// </summary>
    private string PausePushMode()
    {
        if (_autonomousCoordinator == null)
        {
            return "Push mode not enabled.";
        }

        _pushModeCts?.Cancel();
        return "⏸ Push mode paused. Use /resume to continue receiving proposals.";
    }

    /// <summary>
    /// Resumes push mode (continues proposing actions).
    /// </summary>
    private string ResumePushMode()
    {
        if (_autonomousCoordinator == null)
        {
            return "Push mode not enabled. Use --push flag to enable.";
        }

        if (_pushModeCts == null || _pushModeCts.IsCancellationRequested)
        {
            _pushModeCts?.Dispose();
            _pushModeCts = new CancellationTokenSource();
            _pushModeTask = Task.Run(() => _autonomySub.PushModeLoopAsync(_pushModeCts.Token), _pushModeCts.Token);
            return "▶ Push mode resumed. Ouroboros will propose actions.";
        }

        return "Push mode is already active.";
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// SUPPORTING TYPES
// ═══════════════════════════════════════════════════════════════════════════