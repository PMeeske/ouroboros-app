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
#pragma warning disable CS0618 // Obsolete CPE IEmbeddingProvider/IEthicsGate
    private Ouroboros.Core.CognitivePhysics.CognitivePhysicsEngine? _immersiveCogPhysics;
#pragma warning restore CS0618
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
        AnsiConsole.MarkupLine($"\n  [darkcyan][[room→Iaret]] {Markup.Escape(speaker)}: {Markup.Escape(utterance)}[/]");
    }

    // ── Action routing ──────────────────────────────────────────────────────

    /// <summary>
    /// Try to handle skill, tool, or pipeline action commands.
    /// Returns null if not an action command, otherwise returns the result message.
    /// </summary>
    private async Task<string?> TryHandleActionAsync(
        string input,
        ImmersivePersona persona,
        ITextToSpeechService? tts,
        string personaName,
        IVoiceOptions options,
        CancellationToken ct)
    {
        var lower = input.ToLowerInvariant().Trim();

        // List skills
        if (lower is "list skills" or "what skills" or "show skills" or "skills")
        {
            return await HandleListSkillsAsync(personaName);
        }

        // List tokens
        if (lower is "tokens" or "list tokens" or "show tokens" or "pipeline tokens")
        {
            return HandleListTokens(personaName);
        }

        // Pipeline help/examples
        if (lower is "pipeline help" or "pipeline examples" or "help pipeline" or "how to use pipeline" or "pipeline usage")
        {
            return HandlePipelineHelp(personaName);
        }

        // Tool stats
        if (lower is "tool stats" or "toolstats" or "show tool stats")
        {
            return HandleToolStats(personaName);
        }

        // Interconnected learning connections
        if (lower is "connections" or "show connections" or "learning connections" or "tool skill connections")
        {
            return await HandleConnectionsAsync(personaName, ct);
        }

        // Run skill: "run <skillname>" or "execute <skillname>"
        var runMatch = Regex.Match(lower, @"^(run|execute)\s+(.+)$");
        if (runMatch.Success)
        {
            var skillName = runMatch.Groups[2].Value.Trim();
            return await HandleRunSkillAsync(skillName, personaName, options, ct);
        }

        // Google search: "google <query>" or "google search <query>"
        var googleMatch = Regex.Match(lower, @"^google\s*(search)?\s*(.+)$");
        if (googleMatch.Success)
        {
            var query = googleMatch.Groups[2].Value.Trim();
            return await HandleGoogleSearchAsync(query, personaName, ct);
        }

        // Learn about topic: "learn about <topic>"
        var learnMatch = Regex.Match(lower, @"^learn\s+about\s+(.+)$");
        if (learnMatch.Success)
        {
            var topic = learnMatch.Groups[1].Value.Trim();
            return await HandleLearnAboutAsync(topic, personaName, options, ct);
        }

        // Conversational tool creation confirmation: "yes", "ok", "create it", "do it", "build it"
        if (_pendingToolRequest != null && Regex.IsMatch(lower, @"^(yes|ok|create\s*it|do\s*it|build\s*it|make\s*it|go\s*ahead|sure|please)$"))
        {
            var (topic, description) = _pendingToolRequest.Value;
            _pendingToolRequest = null;
            return await HandleCreateToolFromContextAsync(topic, description, personaName, ct);
        }

        // Add tool: "add tool <name>" or "learn <toolname>" or "create tool <name>"
        var toolMatch = Regex.Match(lower, @"^(add\s+tool|learn|create\s+tool|build\s+tool|make\s+tool)\s+(.+)$");
        if (toolMatch.Success && !lower.Contains("about"))
        {
            var toolName = toolMatch.Groups[2].Value.Trim();
            return await HandleAddToolAsync(toolName, personaName, ct);
        }

        // Natural language tool creation: "create a tool that...", "build a tool for...", "make a tool to..."
        var nlToolMatch = Regex.Match(lower, @"^(create|build|make)\s+(a\s+)?tool\s+(that|for|to|which)\s+(.+)$");
        if (nlToolMatch.Success)
        {
            var description = nlToolMatch.Groups[4].Value.Trim();
            return await HandleCreateToolFromDescriptionAsync(description, personaName, ct);
        }

        // Smart tool: "smart tool for <goal>" or "find tool for <goal>"
        var smartMatch = Regex.Match(lower, @"^(smart\s+tool|find\s+tool)\s+(for\s+)?(.+)$");
        if (smartMatch.Success)
        {
            var goal = smartMatch.Groups[3].Value.Trim();
            return await HandleSmartToolAsync(goal, personaName, ct);
        }

        // Memory recall: "remember <topic>", "recall <topic>", "what do you remember about <topic>"
        var rememberMatch = Regex.Match(lower, @"^(remember|recall|what\s+do\s+you\s+remember\s+about)\s+(.+)$");
        if (rememberMatch.Success)
        {
            var topic = rememberMatch.Groups[2].Value.Trim();
            return await HandleMemoryRecallAsync(topic, personaName, ct);
        }

        // Memory stats: "memory stats", "conversation history"
        if (lower is "memory stats" or "conversation history" or "my memory" or "your memory")
        {
            return await HandleMemoryStatsAsync(personaName, ct);
        }

        // Autonomous mind commands
        if (lower is "mind state" or "mind status" or "autonomous state" or "your mind")
        {
            return HandleMindState();
        }

        if (lower is "start mind" or "start thinking" or "wake up mind" or "enable autonomous")
        {
            _autonomousMind?.Start();
            return "🧠 Autonomous mind activated. I'll think, explore the internet, and learn in the background.";
        }

        if (lower is "stop mind" or "stop thinking" or "pause mind" or "disable autonomous")
        {
            if (_autonomousMind != null)
            {
                await _autonomousMind.StopAsync();
            }
            return "💤 Autonomous mind paused. I'll only respond when you talk to me.";
        }

        if (lower is "interests" or "my interests" or "what are you curious about")
        {
            return HandleShowInterests();
        }

        var thinkAboutMatch = Regex.Match(lower, @"^(think about|explore|be curious about|research)\s+(.+)$");
        if (thinkAboutMatch.Success)
        {
            var topic = thinkAboutMatch.Groups[2].Value.Trim();
            _autonomousMind?.InjectTopic(topic);
            _autonomousMind?.AddInterest(topic);
            return $"🤔 I'll explore '{topic}' in the background and let you know if I find something interesting!";
        }

        var addInterestMatch = Regex.Match(lower, @"^(add interest|interest in|i'm interested in)\s+(.+)$");
        if (addInterestMatch.Success)
        {
            var interest = addInterestMatch.Groups[2].Value.Trim();
            _autonomousMind?.AddInterest(interest);
            return $"📌 Added '{interest}' to my interests. I'll keep an eye out for related information!";
        }

        // Reindex commands: "reindex", "reindex full", "reindex incremental"
        if (lower == "reindex" || lower == "reindex full")
        {
            return await HandleFullReindexAsync(personaName, ct);
        }

        if (lower == "reindex incremental" || lower == "reindex inc")
        {
            return await HandleIncrementalReindexAsync(personaName, ct);
        }

        // Index search: "index search <query>" or "search index <query>"
        var indexSearchMatch = Regex.Match(lower, @"^(index\s+search|search\s+index|find\s+in\s+index)\s+(.+)$");
        if (indexSearchMatch.Success)
        {
            var query = indexSearchMatch.Groups[2].Value.Trim();
            return await HandleIndexSearchAsync(query, personaName, ct);
        }

        // Index stats: "index stats" or "indexer stats"
        if (lower == "index stats" || lower == "indexer stats")
        {
            return await HandleIndexStatsAsync(personaName, ct);
        }

        // Emergence: "emergence <topic>"
        var emergenceMatch = Regex.Match(lower, @"^emergence\s+(.+)$");
        if (emergenceMatch.Success)
        {
            var topic = emergenceMatch.Groups[1].Value.Trim();
            return await HandleEmergenceAsync(topic, personaName, options, ct);
        }

        // Pipeline DSL: contains pipe symbol
        if (input.Contains('|'))
        {
            return await HandlePipelineAsync(input, personaName, options, ct);
        }

        // Try to match single pipeline token by name (e.g., "ArxivSearch transformers")
        var singleTokenResult = await TryExecuteSingleTokenAsync(input, personaName, ct);
        if (singleTokenResult != null)
        {
            return singleTokenResult;
        }

        // Try natural language patterns for common tokens
        var nlResult = await TryNaturalLanguageTokenAsync(input, personaName, ct);
        if (nlResult != null)
        {
            return nlResult;
        }

        // Use tool command: "use tool <name> <json_input>" or "tool <name> <json_input>"
        var useToolMatch = Regex.Match(lower, @"^(?:use\s+)?tool\s+(\w+)\s*(.*)$");
        if (useToolMatch.Success)
        {
            var toolName = useToolMatch.Groups[1].Value.Trim();
            var toolInput = useToolMatch.Groups[2].Value.Trim();
            return await HandleUseToolAsync(toolName, toolInput, personaName, ct);
        }

        // List available tools: "list tools" or "what tools" or "show tools"
        if (lower is "list tools" or "what tools" or "show tools" or "tools" or "my tools" or "available tools")
        {
            return HandleListTools(personaName);
        }

        // Self-modification natural language triggers
        if (lower.Contains("modify") && (lower.Contains("your code") || lower.Contains("yourself") || lower.Contains("my code")))
        {
            return HandleSelfModificationHelp(personaName);
        }

        if (lower is "rebuild" or "rebuild yourself" or "recompile" or "build yourself")
        {
            return await HandleUseToolAsync("rebuild_self", "{}", personaName, ct);
        }

        if (lower is "modification history" or "my modifications" or "what did i change" or "view changes")
        {
            return await HandleUseToolAsync("view_modification_history", "{}", personaName, ct);
        }

        // Not an action command
        return null;
    }

    // ── Small utility methods ───────────────────────────────────────────────

    private bool IsExitCommand(string input)
    {
        var lower = input.ToLowerInvariant().Trim();
        return lower is "exit" or "quit" or "bye" or "goodbye" or "leave" or "stop" or "end";
    }

    private string NormalizeEndpoint(string? rawEndpoint, string fallbackEndpoint)
    {
        var endpoint = (rawEndpoint ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return fallbackEndpoint;
        }

        if (!endpoint.Contains("://", StringComparison.Ordinal))
        {
            endpoint = $"http://{endpoint}";
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
        {
            return fallbackEndpoint;
        }

        return uri.ToString().TrimEnd('/');
    }

    private string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (text.Length <= maxLength) return text;
        return text[..(maxLength - 3)] + "...";
    }

    private int LevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
        if (string.IsNullOrEmpty(t)) return s.Length;

        var d = new int[s.Length + 1, t.Length + 1];
        for (int i = 0; i <= s.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= t.Length; j++) d[0, j] = j;

        for (int i = 1; i <= s.Length; i++)
        {
            for (int j = 1; j <= t.Length; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }
        return d[s.Length, t.Length];
    }

    // Causal extraction and graph building consolidated in SharedAgentBootstrap.
    // Call sites use SharedAgentBootstrap.TryExtractCausalTerms / BuildMinimalCausalGraph.
}
