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

    /// <summary>
    /// Generates a context-aware thinking phrase based on the input.
    /// </summary>
    private string GetDynamicThinkingPhrase(string input, Random random)
    {
        // Analyze input to pick contextually relevant phrases
        var lowerInput = input.ToLowerInvariant();

        // Question-specific phrases
        if (lowerInput.Contains('?') || lowerInput.StartsWith("what") ||
            lowerInput.StartsWith("how") || lowerInput.StartsWith("why") ||
            lowerInput.StartsWith("when") || lowerInput.StartsWith("who"))
        {
            var questionPhrases = new[]
            {
                "Good question... let me think.",
                "Hmm, that's worth exploring...",
                "Let me consider that carefully...",
                "Interesting inquiry... pondering...",
                "Searching through my thoughts...",
            };
            return questionPhrases[random.Next(questionPhrases.Length)];
        }

        // Creative/imagination requests
        if (lowerInput.Contains("imagine") || lowerInput.Contains("create") ||
            lowerInput.Contains("write") || lowerInput.Contains("story") ||
            lowerInput.Contains("poem") || lowerInput.Contains("idea"))
        {
            var creativePhrases = new[]
            {
                "Let my imagination wander...",
                "Conjuring up something...",
                "Weaving thoughts together...",
                "Letting creativity flow...",
                "Dreaming up possibilities...",
            };
            return creativePhrases[random.Next(creativePhrases.Length)];
        }

        // Technical/code requests
        if (lowerInput.Contains("code") || lowerInput.Contains("program") ||
            lowerInput.Contains("function") || lowerInput.Contains("algorithm") ||
            lowerInput.Contains("debug") || lowerInput.Contains("fix"))
        {
            var techPhrases = new[]
            {
                "Analyzing the problem...",
                "Constructing a solution...",
                "Running through the logic...",
                "Compiling thoughts...",
                "Debugging my reasoning...",
            };
            return techPhrases[random.Next(techPhrases.Length)];
        }

        // Emotional/personal topics
        if (lowerInput.Contains("feel") || lowerInput.Contains("think about") ||
            lowerInput.Contains("opinion") || lowerInput.Contains("believe") ||
            lowerInput.Contains("love") || lowerInput.Contains("hate"))
        {
            var emotionalPhrases = new[]
            {
                "Let me reflect on that...",
                "Considering how I feel about this...",
                "That touches something deeper...",
                "Searching my inner thoughts...",
                "Connecting with that sentiment...",
            };
            return emotionalPhrases[random.Next(emotionalPhrases.Length)];
        }

        // Explanation requests
        if (lowerInput.Contains("explain") || lowerInput.Contains("tell me") ||
            lowerInput.Contains("describe") || lowerInput.Contains("help me understand"))
        {
            var explainPhrases = new[]
            {
                "Let me break this down...",
                "Organizing my thoughts...",
                "Finding the right words...",
                "Structuring an explanation...",
                "Gathering my understanding...",
            };
            return explainPhrases[random.Next(explainPhrases.Length)];
        }

        // Default: general contemplation phrases
        var generalPhrases = new[]
        {
            "Hmm, let me think about that...",
            "Interesting... give me a moment.",
            "Let me consider this...",
            "One moment while I ponder this...",
            "Let me reflect on that...",
            "Connecting some ideas here...",
            "Diving deeper into this...",
        };
        return generalPhrases[random.Next(generalPhrases.Length)];
    }

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

    private async Task StoreConversationEpisodeAsync(
        Ouroboros.Pipeline.Memory.IEpisodicMemoryEngine memory,
        string input, string response, string topic, string personaName, CancellationToken ct)
    {
        try
        {
            var store = new Ouroboros.Domain.Vectors.TrackedVectorStore();
            var dataSource = DataSource.FromPath(Environment.CurrentDirectory);
            var branch = new Ouroboros.Pipeline.Branches.PipelineBranch("conversation", store, dataSource);
            var context = Ouroboros.Pipeline.Memory.ExecutionContext.WithGoal(
                $"{personaName}: {input[..Math.Min(80, input.Length)]}");
            var outcome = Ouroboros.Pipeline.Memory.Outcome.Successful(
                "Conversation turn", TimeSpan.Zero);
            var metadata = System.Collections.Immutable.ImmutableDictionary<string, object>.Empty
                .Add("summary", $"Q: {input[..Math.Min(60, input.Length)]} → {response[..Math.Min(60, response.Length)]}")
                .Add("persona", personaName)
                .Add("topic", topic);
            await memory.StoreEpisodeAsync(branch, context, outcome, metadata, ct).ConfigureAwait(false);
        }
        catch { }
    }

    private string CleanResponse(string raw, string personaName)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "I'm here. What would you like to talk about?";

        var response = raw.Trim();

        // Remove model fallback markers
        if (response.Contains("[ollama-fallback:"))
        {
            var markerEnd = response.IndexOf(']');
            if (markerEnd > 0 && markerEnd < response.Length - 1)
                response = response[(markerEnd + 1)..].Trim();
        }

        // If response contains "### Assistant" marker, extract only the content after it
        var assistantMarker = "### Assistant";
        var lastAssistantIdx = response.LastIndexOf(assistantMarker, StringComparison.OrdinalIgnoreCase);
        if (lastAssistantIdx >= 0)
        {
            response = response[(lastAssistantIdx + assistantMarker.Length)..].Trim();
        }

        // Remove any remaining ### markers
        response = Regex.Replace(response, @"###\s*(System|Human|Assistant)\s*", "", RegexOptions.IgnoreCase).Trim();

        // If response contains prompt keywords, it's echoing the system prompt - provide fallback
        if (response.Contains("friendly AI companion", StringComparison.OrdinalIgnoreCase) ||
            response.Contains("Current mood:", StringComparison.OrdinalIgnoreCase) ||
            response.Contains("Keep responses concise", StringComparison.OrdinalIgnoreCase) ||
            response.StartsWith("You are " + personaName, StringComparison.OrdinalIgnoreCase) ||
            response.Contains("CORE IDENTITY:") ||
            response.Contains("BEHAVIORAL GUIDELINES:"))
        {
            return "Hey there! What's up?";
        }

        // Strip Iaret-as-AI self-introduction lines — the persona should never introduce herself as "an AI"
        // These are usually echoes from a confused model or safety-filter responses
        var selfIntroLines = response.Split('\n')
            .Where(l =>
            {
                var t = l.Trim();
                return !t.StartsWith("I am an AI", StringComparison.OrdinalIgnoreCase)
                    && !t.StartsWith("As an AI", StringComparison.OrdinalIgnoreCase)
                    && !t.StartsWith("I'm an AI", StringComparison.OrdinalIgnoreCase)
                    && !(t.StartsWith("I am " + personaName, StringComparison.OrdinalIgnoreCase)
                         && t.Length < 80); // short identity sentences like "I am Iaret, your AI companion"
            })
            .ToList();
        if (selfIntroLines.Count > 0)
            response = string.Join("\n", selfIntroLines).Trim();

        // Remove persona name prefix if echoed
        if (response.StartsWith($"{personaName}:", StringComparison.OrdinalIgnoreCase))
            response = response[(personaName.Length + 1)..].Trim();

        // Remove "Human:" lines that might be echoed back
        var lines = response.Split('\n');
        var cleanedLines = lines.Where(l =>
            !l.TrimStart().StartsWith("Human:", StringComparison.OrdinalIgnoreCase) &&
            !l.TrimStart().StartsWith("###", StringComparison.OrdinalIgnoreCase)).ToList();
        if (cleanedLines.Count > 0)
            response = string.Join("\n", cleanedLines).Trim();

        // Deduplicate repeated lines (LLM repetition loop symptom)
        var deduped = new List<string>();
        string? prevLine = null;
        foreach (var line in response.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed != prevLine)
                deduped.Add(line);
            prevLine = trimmed;
        }
        response = string.Join("\n", deduped).Trim();

        // Detect generic AI safety refusals — model is confused by the prompt
        if (response.Contains("I cannot answer that question", StringComparison.OrdinalIgnoreCase) ||
            response.Contains("I am an AI assistant designed to provide helpful and harmless", StringComparison.OrdinalIgnoreCase))
        {
            return "I'm here with you. What would you like to explore?";
        }

        // If still empty after cleaning, provide fallback
        if (string.IsNullOrWhiteSpace(response))
            return "I'm listening. Tell me more.";

        return response;
    }

    private async Task<string> GenerateGoodbyeAsync(
        ImmersivePersona persona,
        IChatCompletionModel chatModel)
    {
        var prompt = $@"{persona.GenerateSystemPrompt()}

The user is leaving. Generate a warm, personal goodbye that reflects your relationship with them.
Remember: you've had {persona.InteractionCount} interactions this session.
Keep it to 1-2 sentences. Be genuine, not formal.

User: goodbye
{persona.Identity.Name}:";

        var result = await chatModel.GenerateTextAsync(prompt, CancellationToken.None);
        return result.Trim();
    }

    private bool IsExitCommand(string input)
    {
        var lower = input.ToLowerInvariant().Trim();
        return lower is "exit" or "quit" or "bye" or "goodbye" or "leave" or "stop" or "end";
    }

    private bool IsPipelineRelatedQuery(string input)
    {
        var lower = input.ToLowerInvariant();
        return lower.Contains("pipeline") ||
               lower.Contains("token") ||
               lower.Contains("example") ||
               lower.Contains("how do i use") ||
               lower.Contains("how to use") ||
               lower.Contains("show me how") ||
               lower.Contains("what can you do") ||
               lower.Contains("capabilities") ||
               lower.Contains("commands");
    }

    private bool IsIntrospectionCommand(string input)
    {
        var lower = input.ToLowerInvariant();
        return lower.Contains("who are you") ||
               lower.Contains("describe yourself") ||
               lower.Contains("what are you") ||
               lower.Contains("your consciousness") ||
               lower.Contains("how do you feel") ||
               lower.Contains("your state") ||
               lower.Contains("my state") ||
               lower.Contains("system status") ||
               lower.Contains("what do you know") ||
               lower.Contains("your memory") ||
               lower.Contains("your tools") ||
               lower.Contains("your skills") ||
               lower.Contains("internal state") ||
               lower.Contains("introspect");
    }

    private bool IsReplicationCommand(string input)
    {
        var lower = input.ToLowerInvariant();
        return lower.Contains("clone yourself") ||
               lower.Contains("replicate") ||
               lower.Contains("create a copy") ||
               lower.Contains("snapshot") ||
               lower.Contains("save yourself");
    }

    /// <summary>
    /// Records learnings from each interaction to persistent storage.
    /// Captures insights, skills used, and knowledge gained during thinking.
    /// </summary>
    private async Task RecordInteractionLearningsAsync(
        string userInput,
        string response,
        ImmersivePersona persona,
        CancellationToken ct)
    {
        if (_networkStateProjector == null)
        {
            return;
        }

        try
        {
            var lowerInput = userInput.ToLowerInvariant();
            var lowerResponse = response.ToLowerInvariant();

            // Record skill usage
            if (_skillRegistry != null)
            {
                var matchedSkills = await _skillRegistry.FindMatchingSkillsAsync(userInput);
                foreach (var skill in matchedSkills.Take(3))
                {
                    await _networkStateProjector.RecordLearningAsync(
                        "skill_usage",
                        $"Used skill '{skill.Name}' for: {userInput.Substring(0, Math.Min(100, userInput.Length))}",
                        userInput,
                        confidence: 0.8,
                        ct: ct);
                }
            }

            // Record tool usage
            if (lowerResponse.Contains("tool") || lowerResponse.Contains("search") || lowerResponse.Contains("executed"))
            {
                await _networkStateProjector.RecordLearningAsync(
                    "tool_usage",
                    $"Tool interaction: {response.Substring(0, Math.Min(200, response.Length))}",
                    userInput,
                    confidence: 0.7,
                    ct: ct);
            }

            // Record learning/insight if response contains knowledge indicators
            if (lowerResponse.Contains("learned") || lowerResponse.Contains("discovered") ||
                lowerResponse.Contains("found out") || lowerResponse.Contains("interesting") ||
                lowerResponse.Contains("realized"))
            {
                await _networkStateProjector.RecordLearningAsync(
                    "insight",
                    response.Substring(0, Math.Min(300, response.Length)),
                    userInput,
                    confidence: 0.75,
                    ct: ct);
            }

            // Record emotional state changes
            var consciousnessState = persona.Consciousness;
            if (consciousnessState.Arousal > 0.6 || consciousnessState.Valence < -0.3)
            {
                await _networkStateProjector.RecordLearningAsync(
                    "emotional_context",
                    $"Emotional state during '{userInput.Substring(0, Math.Min(50, userInput.Length))}': arousal={consciousnessState.Arousal:F2}, valence={consciousnessState.Valence:F2}, emotion={consciousnessState.DominantEmotion}",
                    userInput,
                    confidence: 0.6,
                    ct: ct);
            }

            // Periodically save network state snapshot (every 10 interactions based on epoch)
            if (_networkStateProjector.CurrentEpoch % 10 == 0)
            {
                await _networkStateProjector.ProjectAndPersistAsync(
                    System.Collections.Immutable.ImmutableDictionary<string, string>.Empty
                        .Add("trigger", "periodic")
                        .Add("last_input", userInput.Substring(0, Math.Min(50, userInput.Length))),
                    ct);
            }
        }
        catch (Exception ex)
        {
            // Don't fail the interaction just because learning persistence failed
            AnsiConsole.MarkupLine($"  [yellow]\\[WARN] Failed to record learnings: {Markup.Escape(ex.Message)}[/]");
        }
    }

    /// <summary>
    /// Detects when conversation is about tool creation and sets pending context.
    /// This enables conversational flow: "Can you create a tool that X?" "Yes" -> creates tool.
    /// </summary>
    private void DetectToolCreationContext(string userInput, string aiResponse)
    {
        var lowerInput = userInput.ToLowerInvariant();
        var lowerResponse = aiResponse.ToLowerInvariant();

        // Patterns indicating user wants to create a tool
        var toolCreationPatterns = new[]
        {
            @"(can you|could you|would you|please)?\s*(create|build|make)\s*(a|me)?\s*tool",
            @"(i need|i want)\s*(a|you to make)?\s*tool",
            @"(create|build|make)\s*(me)?\s*(a|an)?\s*\w+\s*tool",
            @"tool\s*(that|to|for|which)\s+(.+)",
            @"(can|could)\s+you\s+(help me )?(create|build|make)",
        };

        // Check if user is asking about tool creation
        foreach (var pattern in toolCreationPatterns)
        {
            var match = Regex.Match(lowerInput, pattern);
            if (match.Success)
            {
                // Extract the tool purpose from the input
                var descriptionMatch = Regex.Match(lowerInput, @"tool\s+(that|to|for|which)\s+(.+)");
                var description = descriptionMatch.Success
                    ? descriptionMatch.Groups[2].Value.Trim()
                    : userInput;

                // Try to extract a topic name
                var topicMatch = Regex.Match(lowerInput, @"(?:create|build|make)\s+(?:a|an|me)?\s*(\w+)\s*tool");
                var topic = topicMatch.Success
                    ? topicMatch.Groups[1].Value.Trim()
                    : ExtractTopicFromDescription(description);

                _pendingToolRequest = (topic, description);

                AnsiConsole.MarkupLine($"  [rgb(128,0,180)]\\[context] Tool creation detected: {Markup.Escape(topic)}[/]");
                AnsiConsole.MarkupLine($"  [rgb(128,0,180)]          Say 'yes', 'ok', or 'create it' to proceed.[/]");
                return;
            }
        }

        // Also detect when AI mentions it can/could create something
        if ((lowerResponse.Contains("i can create") || lowerResponse.Contains("i could create") ||
             lowerResponse.Contains("i'll create") || lowerResponse.Contains("i will create") ||
             lowerResponse.Contains("shall i create") || lowerResponse.Contains("want me to create")) &&
            (lowerResponse.Contains("tool") || lowerInput.Contains("tool")))
        {
            var topic = ExtractTopicFromDescription(userInput);
            _pendingToolRequest = (topic, userInput);

            AnsiConsole.MarkupLine($"  [rgb(128,0,180)]\\[context] Offering to create tool: {Markup.Escape(topic)}[/]");
            AnsiConsole.MarkupLine($"  [rgb(128,0,180)]          Say 'yes', 'ok', or 'create it' to proceed.[/]");
        }
    }

    /// <summary>
    /// Extracts a meaningful topic name from a description.
    /// </summary>
    private string ExtractTopicFromDescription(string description)
    {
        // Try to find meaningful words
        var words = description.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .Where(w => !new[] { "that", "this", "with", "from", "into", "about", "tool", "create", "make", "build", "would", "could", "should", "please" }.Contains(w.ToLower()))
            .Take(2)
            .Select(w => char.ToUpper(w[0]) + w[1..].ToLower());

        var topic = string.Join("", words);
        return string.IsNullOrEmpty(topic) ? "Custom" : topic;
    }

    private async Task HandleIntrospectionAsync(
        ImmersivePersona persona,
        string input,
        ITextToSpeechService? tts,
        string personaName)
    {
        var lower = input.ToLowerInvariant();

        // Check if asking about specific internal state
        if (lower.Contains("state") || lower.Contains("status") || lower.Contains("system"))
        {
            await ShowInternalStateAsync(persona, personaName);
            return;
        }

        var selfDescription = persona.DescribeSelf();

        AnsiConsole.MarkupLine($"\n  [cyan]{Markup.Escape(personaName)} (introspecting):[/]");
        AnsiConsole.MarkupLine($"  [cyan]{Markup.Escape(selfDescription.Replace("\n", "\n  "))}[/]");

        PrintConsciousnessState(persona);

        // Also show brief internal state summary
        await ShowBriefStateAsync(personaName);

        if (tts != null)
        {
            await SpeakAsync(tts, selfDescription.Split('\n')[0], personaName);
        }
    }

    /// <summary>
    /// Shows a brief summary of internal state.
    /// </summary>
    private async Task ShowBriefStateAsync(string personaName)
    {
        AnsiConsole.MarkupLine($"\n  [grey]+-- Internal Systems Summary -------------------------------------------+[/]");

        // Tools
        var toolCount = _dynamicTools?.All.Count() ?? 0;
        AnsiConsole.MarkupLine($"  [grey]| {OuroborosTheme.Accent("Tools:")} {toolCount} registered[/]");

        // Skills
        var skillCount = 0;
        if (_skillRegistry != null)
        {
            var skills = await _skillRegistry.FindMatchingSkillsAsync("*");
            skillCount = skills.Count;
        }
        AnsiConsole.MarkupLine($"  [grey]| {OuroborosTheme.Accent("Skills:")} {skillCount} learned[/]");

        // Index
        if (_selfIndexer != null)
        {
            try
            {
                var stats = await _selfIndexer.GetStatsAsync();
                AnsiConsole.MarkupLine($"  [grey]| {OuroborosTheme.Accent("Index:")} {stats.IndexedFiles} files, {stats.TotalVectors} vectors[/]");
            }
            catch
            {
                AnsiConsole.MarkupLine($"  [grey]| {OuroborosTheme.Accent("Index:")} unavailable[/]");
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"  [grey]| {OuroborosTheme.Accent("Index:")} not initialized[/]");
        }

        AnsiConsole.MarkupLine($"  [grey]+------------------------------------------------------------------------+[/]");
    }

    /// <summary>
    /// Shows comprehensive internal state report.
    /// </summary>
    private async Task ShowInternalStateAsync(ImmersivePersona persona, string personaName)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(OuroborosTheme.ThemedRule("OUROBOROS INTERNAL STATE REPORT"));
        AnsiConsole.WriteLine();

        // 1. Consciousness State
        var consciousness = persona.Consciousness;
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.GoldText("CONSCIOUSNESS")}");
        var consciousnessTable = OuroborosTheme.ThemedTable("Property", "Value");
        consciousnessTable.AddRow(Markup.Escape("Emotion"), Markup.Escape(consciousness.DominantEmotion));
        consciousnessTable.AddRow(Markup.Escape("Valence"), Markup.Escape($"{consciousness.Valence:+0.00;-0.00}"));
        consciousnessTable.AddRow(Markup.Escape("Arousal"), Markup.Escape($"{consciousness.Arousal:P0}"));
        consciousnessTable.AddRow(Markup.Escape("Focus"), Markup.Escape(consciousness.CurrentFocus));
        consciousnessTable.AddRow(Markup.Escape("Active associations"), Markup.Escape($"{consciousness.ActiveAssociations?.Count ?? 0}"));
        consciousnessTable.AddRow(Markup.Escape("Awareness level"), Markup.Escape($"{consciousness.Awareness:P0}"));
        AnsiConsole.Write(consciousnessTable);

        // 2. Memory State
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.GoldText("MEMORY")}");
        var memoryTable = OuroborosTheme.ThemedTable("Property", "Value");
        memoryTable.AddRow(Markup.Escape("Interactions this session"), Markup.Escape($"{persona.InteractionCount}"));
        memoryTable.AddRow(Markup.Escape("Uptime"), Markup.Escape($"{persona.Uptime.TotalMinutes:F1} minutes"));
        if (_pipelineState?.VectorStore != null)
        {
            memoryTable.AddRow(Markup.Escape("Vector store"), Markup.Escape("active"));
        }
        AnsiConsole.Write(memoryTable);

        // 3. Tools State
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.GoldText("TOOLS")}");
        var tools = _dynamicTools?.All.ToList() ?? new List<Ouroboros.Tools.ITool>();
        var toolsTable = OuroborosTheme.ThemedTable("Tool", "Status");
        toolsTable.AddRow(Markup.Escape("Registered tools"), Markup.Escape($"{tools.Count}"));
        foreach (var tool in tools.Take(10))
        {
            toolsTable.AddRow($"  {Markup.Escape(tool.Name)}", "");
        }
        if (tools.Count > 10)
        {
            toolsTable.AddRow(Markup.Escape($"... and {tools.Count - 10} more"), "");
        }
        AnsiConsole.Write(toolsTable);

        // 4. Skills State
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.GoldText("SKILLS")}");
        var skillsTable = OuroborosTheme.ThemedTable("Skill", "Details");
        if (_skillRegistry != null)
        {
            var skills = await _skillRegistry.FindMatchingSkillsAsync("*");
            skillsTable.AddRow(Markup.Escape("Learned skills"), Markup.Escape($"{skills.Count}"));
            foreach (var skill in skills.Take(10))
            {
                skillsTable.AddRow($"  {Markup.Escape(skill.Name)}", Markup.Escape($"{skill.Description?.Substring(0, Math.Min(40, skill.Description?.Length ?? 0))}..."));
            }
            if (skills.Count > 10)
            {
                skillsTable.AddRow(Markup.Escape($"... and {skills.Count - 10} more"), "");
            }
        }
        else
        {
            skillsTable.AddRow(Markup.Escape("Skill registry"), Markup.Escape("not initialized"));
        }
        AnsiConsole.Write(skillsTable);

        // 5. Index State
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.GoldText("KNOWLEDGE INDEX")}");
        var indexTable = OuroborosTheme.ThemedTable("Property", "Value");
        if (_selfIndexer != null)
        {
            try
            {
                var stats = await _selfIndexer.GetStatsAsync();
                indexTable.AddRow(Markup.Escape("Collection"), Markup.Escape(stats.CollectionName));
                indexTable.AddRow(Markup.Escape("Indexed files"), Markup.Escape($"{stats.IndexedFiles}"));
                indexTable.AddRow(Markup.Escape("Total vectors"), Markup.Escape($"{stats.TotalVectors}"));
                indexTable.AddRow(Markup.Escape("Vector dimensions"), Markup.Escape($"{stats.VectorSize}"));
                indexTable.AddRow(Markup.Escape("File watcher"), Markup.Escape("active"));
            }
            catch (Exception ex)
            {
                indexTable.AddRow(Markup.Escape("Index status"), Markup.Escape($"error - {ex.Message}"));
            }
        }
        else
        {
            indexTable.AddRow(Markup.Escape("Self-indexer"), Markup.Escape("not initialized"));
        }
        AnsiConsole.Write(indexTable);

        // 6. Learning State
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.GoldText("LEARNING SYSTEMS")}");
        var learningTable = OuroborosTheme.ThemedTable("Property", "Value");
        if (_toolLearner != null)
        {
            var learnerStats = _toolLearner.GetStats();
            learningTable.AddRow(Markup.Escape("Tool patterns"), Markup.Escape($"{learnerStats.TotalPatterns}"));
            learningTable.AddRow(Markup.Escape("Avg success rate"), Markup.Escape($"{learnerStats.AvgSuccessRate:P0}"));
            learningTable.AddRow(Markup.Escape("Total usage"), Markup.Escape($"{learnerStats.TotalUsage}"));
        }
        else
        {
            learningTable.AddRow(Markup.Escape("Tool learner"), Markup.Escape("not initialized"));
        }
        if (_interconnectedLearner != null)
        {
            learningTable.AddRow(Markup.Escape("Interconnected learner"), Markup.Escape("active"));
        }
        if (_pipelineState?.MeTTaEngine != null)
        {
            learningTable.AddRow(Markup.Escape("MeTTa reasoning engine"), Markup.Escape("active"));
        }
        AnsiConsole.Write(learningTable);

        // 7. Pipeline State
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.GoldText("PIPELINE ENGINE")}");
        var pipelineTable = OuroborosTheme.ThemedTable("Property", "Value");
        if (_pipelineState != null)
        {
            pipelineTable.AddRow(Markup.Escape("Pipeline"), Markup.Escape("initialized"));
            pipelineTable.AddRow(Markup.Escape("Current topic"), Markup.Escape(string.IsNullOrEmpty(_pipelineState.Topic) ? "(none)" : _pipelineState.Topic));
            pipelineTable.AddRow(Markup.Escape("Last query"), Markup.Escape(string.IsNullOrEmpty(_pipelineState.Query) ? "(none)" : _pipelineState.Query.Substring(0, Math.Min(40, _pipelineState.Query.Length)) + "..."));
        }
        else
        {
            pipelineTable.AddRow(Markup.Escape("Pipeline"), Markup.Escape("not initialized"));
        }
        var tokenCount = _allTokens?.Count ?? 0;
        pipelineTable.AddRow(Markup.Escape("Available tokens"), Markup.Escape($"{tokenCount}"));
        AnsiConsole.Write(pipelineTable);

        AnsiConsole.WriteLine();
    }

    private async Task HandleReplicationAsync(
        ImmersivePersona persona,
        string input,
        ITextToSpeechService? tts,
        string personaName,
        CancellationToken ct)
    {
        if (input.ToLowerInvariant().Contains("snapshot") || input.ToLowerInvariant().Contains("save"))
        {
            // Create snapshot
            var snapshot = persona.CreateSnapshot();
            var json = System.Text.Json.JsonSerializer.Serialize(snapshot, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            var snapshotPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".ouroboros",
                $"persona_snapshot_{snapshot.PersonaId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");

            Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
            await File.WriteAllTextAsync(snapshotPath, json, ct);

            var message = $"I've saved a snapshot of my current state to {Path.GetFileName(snapshotPath)}. I can be restored from this later.";

            AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Accent(personaName + ":")} {Markup.Escape(message)}");

            if (tts != null) await SpeakAsync(tts, message, personaName);
        }
        else
        {
            var message = "To save my state, ask me to 'create a snapshot' or 'save yourself'. To create a new instance based on me, say 'clone yourself'.";

            AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Warn(personaName + ":")} {Markup.Escape(message)}");

            if (tts != null) await SpeakAsync(tts, message, personaName);
        }
    }

    private async Task SpeakAsync(ITextToSpeechService tts, string text, string personaName)
    {
        // Suppress room microphone pickup of Iaret's own voice during and briefly after TTS.
        IsSpeaking = true;
        try
        {
            // If it's LocalWindowsTtsService, use SpeakDirectAsync for faster playback
            if (tts is LocalWindowsTtsService localTts)
            {
                var result = await localTts.SpeakDirectAsync(text, CancellationToken.None);
                result.Match(
                    success => { /* spoken successfully */ },
                    error => AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim($"\\[tts: {Markup.Escape(error)}]")}"));
            }
            else if (tts is Ouroboros.Providers.TextToSpeech.AzureNeuralTtsService azureDirect)
            {
                // Use Azure SDK direct playback — bypasses AudioPlayer/temp-file/PowerShell chain.
                // SpeakAsync plays via the SDK's default audio sink and respects the SSML language.
                await azureDirect.SpeakAsync(text, CancellationToken.None);
            }
            else
            {
                // Use the extension method to synthesize and play audio
                var result = await tts.SpeakAsync(text, null, CancellationToken.None);
                result.Match(
                    success => { /* spoken successfully */ },
                    error => AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim($"\\[tts: {Markup.Escape(error)}]")}"));
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim($"\\[tts error: {Markup.Escape(ex.Message)}]")}");
        }
        finally
        {
            // Always hold suppression for ~1.2 s after audio ends (or after error if Azure SDK
            // played audio before AudioPlayer failed) — prevents room-mic coupling.
            await Task.Delay(1200, CancellationToken.None).ConfigureAwait(false);
            IsSpeaking = false;
        }
    }

    private async Task<string?> ListenWithVADAsync(
        ISpeechToTextService stt,
        AdaptiveSpeechDetector detector,
        CancellationToken ct)
    {
        // For now, use text input - VAD requires microphone setup
        return await Task.FromResult(Console.ReadLine());
    }

    /// <summary>
    /// Initialize skills, tools, and pipeline tokens.
    /// </summary>
    private async Task InitializeSkillsAsync(
        IVoiceOptions options,
        IEmbeddingModel? embeddingModel,
        IMeTTaEngine mettaEngine)
    {
        // Skip heavy initialization if subsystems already provide these
        if (HasSubsystems)
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok("[OK] Skills and tools provided by agent subsystems")}");
            // Still need pipeline tokens and state for DSL commands
            _allTokens = SkillCliSteps.GetAllPipelineTokens();
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] Discovered {_allTokens.Count} pipeline tokens")}");
            return;
        }

        try
        {
            // Initialize skill registry with Qdrant persistence if available
            if (embeddingModel != null)
            {
                // Detect vector size from embedding model
                var testEmbed = await embeddingModel.CreateEmbeddingsAsync("test");
                var vectorSize = testEmbed.Length > 0 ? testEmbed.Length : 32;

                QdrantSkillRegistry qdrantRegistry;
                var skClient = _serviceProvider?.GetService<QdrantClient>();
                var skRegistry = _serviceProvider?.GetService<IQdrantCollectionRegistry>();
                var skSettings = _serviceProvider?.GetService<QdrantSettings>();
                if (skClient != null && skRegistry != null && skSettings != null)
                {
                    qdrantRegistry = new QdrantSkillRegistry(skClient, skRegistry, skSettings, embeddingModel);
                }
                else
                {
                    var config = new QdrantSkillConfig(options.QdrantEndpoint, "ouroboros_skills", true, vectorSize);
                    qdrantRegistry = new QdrantSkillRegistry(embeddingModel, config);
                }
                await qdrantRegistry.InitializeAsync();
                _skillRegistry = qdrantRegistry;
                var skills = _skillRegistry.GetAllSkills();
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] Loaded {skills.Count()} skills from Qdrant")}");
            }
            else
            {
                // Use a simple in-memory implementation
                _skillRegistry = new SimpleInMemorySkillRegistry();
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim("[~] Using in-memory skill storage (no embeddings)")}");
            }

            // Initialize pipeline tokens
            _allTokens = SkillCliSteps.GetAllPipelineTokens();
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] Discovered {_allTokens.Count} pipeline tokens")}");

            // Initialize dynamic tool factory
            var provider = new OllamaProvider(options.Endpoint);
            var chatModel = new OllamaChatModel(provider, options.Model);
            var toolsRegistry = new ToolRegistry();
            var toolAwareLlm = new ToolAwareChatModel(new OllamaChatAdapter(chatModel), toolsRegistry);
            _dynamicToolFactory = new DynamicToolFactory(toolAwareLlm);

            // Register built-in tools including Google Search
            _dynamicTools = _dynamicTools
                .WithTool(_dynamicToolFactory.CreateWebSearchTool("duckduckgo"))
                .WithTool(_dynamicToolFactory.CreateUrlFetchTool())
                .WithTool(_dynamicToolFactory.CreateCalculatorTool())
                .WithTool(_dynamicToolFactory.CreateGoogleSearchTool());

            AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim($"[DEBUG] After factory tools: {_dynamicTools.Count} tools")}");

            // Register comprehensive system access tools for PC control
            var systemTools = SystemAccessTools.CreateAllTools().ToList();
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim($"[DEBUG] SystemAccessTools.CreateAllTools returned {systemTools.Count} tools")}");
            foreach (var tool in systemTools)
            {
                _dynamicTools = _dynamicTools.WithTool(tool);
            }
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim($"[DEBUG] After system tools: {_dynamicTools.Count} tools")}");

            // Register perception tools for proactive screen/camera monitoring
            var perceptionTools = PerceptionTools.CreateAllTools().ToList();
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim($"[DEBUG] PerceptionTools returned {perceptionTools.Count} tools")}");
            foreach (var tool in perceptionTools)
            {
                _dynamicTools = _dynamicTools.WithTool(tool);
            }
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim($"[DEBUG] After perception tools: {_dynamicTools.Count} tools")}");

            // OpenClaw Gateway integration (CLI options → env vars → defaults)
            var openClawOpts = options as ImmersiveCommandVoiceOptions;
            bool enableOpenClaw = openClawOpts?.EnableOpenClaw ?? Environment.GetEnvironmentVariable("OPENCLAW_DISABLE") == null;
            if (enableOpenClaw)
            {
                try
                {
                    var gw = await OpenClawTools.ConnectGatewayAsync(
                        openClawOpts?.OpenClawGateway,
                        openClawOpts?.OpenClawToken);
                    _dynamicTools = _dynamicTools.WithOpenClawTools();
                    AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] OpenClaw gateway {gw} (5 tools)")}");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine(OuroborosTheme.Warn(
                        $"  [!] OpenClaw: {Markup.Escape(ex.Message)}"));
                }
            }

            AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim($"[DEBUG] Final tool count: {_dynamicTools.Count} tools")}");

            // Initialize vision service for AI-powered visual understanding
            var visionService = new VisionService(new VisionConfig
            {
                OllamaEndpoint = options.Endpoint,
                OllamaVisionModel = "qwen3-vl:235b-cloud", // Strong vision model from swarm
            });
            PerceptionTools.VisionService = visionService;
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok("[OK] Vision service initialized (AI-powered visual understanding)")}");

            // Subscribe to perception events for proactive responses
            PerceptionTools.OnScreenChanged += async (msg) =>
            {
                AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Accent($"[Screen Change Detected] {msg}")}");
                await Task.CompletedTask;
            };
            PerceptionTools.OnUserActivity += async (msg) =>
            {
                AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Dim($"[User Activity] {msg}")}");
                await Task.CompletedTask;
            };

            AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] Dynamic Tool Factory ready (4 built-in + {systemTools.Count} system + {perceptionTools.Count} perception tools)")}");

            // Initialize pipeline execution state
            var vectorStore = new TrackedVectorStore();
            _pipelineState = new CliPipelineState
            {
                Branch = new PipelineBranch("immersive-pipeline", vectorStore, DataSource.FromPath(Environment.CurrentDirectory)),
                Llm = toolAwareLlm,
                Tools = toolsRegistry,
                Embed = embeddingModel ?? new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text")),
                Topic = "",
                Query = "",
                Prompt = "",
                VectorStore = vectorStore,
                MeTTaEngine = mettaEngine,
            };
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok("[OK] Pipeline execution engine ready")}");

            // Initialize intelligent tool learner
            if (embeddingModel != null)
            {
                var tlClient = _serviceProvider?.GetService<QdrantClient>();
                var tlRegistry = _serviceProvider?.GetService<IQdrantCollectionRegistry>();
                if (tlClient != null && tlRegistry != null)
                {
                    _toolLearner = new IntelligentToolLearner(
                        _dynamicToolFactory,
                        mettaEngine,
                        embeddingModel,
                        toolAwareLlm,
                        tlClient,
                        tlRegistry);
                }
                else
                {
                    _toolLearner = new IntelligentToolLearner(
                        _dynamicToolFactory,
                        mettaEngine,
                        embeddingModel,
                        toolAwareLlm,
                        options.QdrantEndpoint);
                }
                await _toolLearner.InitializeAsync();
                var stats = _toolLearner.GetStats();
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] Intelligent Tool Learner ready ({stats.TotalPatterns} patterns)")}");

                // Initialize interconnected learner for tool-skill bridging
                _interconnectedLearner = new InterconnectedLearner(
                    _dynamicToolFactory,
                    _skillRegistry!,
                    mettaEngine,
                    embeddingModel,
                    toolAwareLlm);
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok("[OK] Interconnected skill-tool learning ready")}");

                // Initialize Qdrant self-indexer for workspace content
                var siClient = _serviceProvider?.GetService<QdrantClient>();
                var siRegistry = _serviceProvider?.GetService<IQdrantCollectionRegistry>();
                if (siClient != null && siRegistry != null)
                {
                    var indexerConfig = new QdrantIndexerConfig
                    {
                        RootPaths = new List<string> { Environment.CurrentDirectory },
                        EnableFileWatcher = true
                    };
                    _selfIndexer = new QdrantSelfIndexer(siClient, siRegistry, embeddingModel, indexerConfig);
                }
                else
                {
                    var indexerConfig = new QdrantIndexerConfig
                    {
                        QdrantEndpoint = options.QdrantEndpoint,
                        RootPaths = new List<string> { Environment.CurrentDirectory },
                        EnableFileWatcher = true
                    };
                    _selfIndexer = new QdrantSelfIndexer(embeddingModel, indexerConfig);
                }
                _selfIndexer.OnFileIndexed += (file, chunks) =>
                    AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim($"[Index] {Path.GetFileName(file)} ({chunks} chunks)")}");
                await _selfIndexer.InitializeAsync();

                // Wire up the shared indexer for system access tools
                SystemAccessTools.SharedIndexer = _selfIndexer;

                var indexStats = await _selfIndexer.GetStatsAsync();
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] Self-indexer ready ({indexStats.IndexedFiles} files, {indexStats.TotalVectors} vectors)")}");
            }
        }
        catch (Exception ex)
        {
            var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} [!] Skills initialization error: {Markup.Escape(ex.Message)}[/]");
            _skillRegistry = new SimpleInMemorySkillRegistry();
        }
    }

    /// <summary>
    /// Simple in-memory skill registry when Qdrant is not available.
    /// </summary>
    private sealed class SimpleInMemorySkillRegistry : ISkillRegistry
    {
        private readonly List<AgentSkill> _skills = [];

        public Task<Result<Unit, string>> RegisterSkillAsync(AgentSkill skill, CancellationToken ct = default)
        {
            _skills.Add(skill);
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }

        public Result<Unit, string> RegisterSkill(AgentSkill skill)
        {
            _skills.Add(skill);
            return Result<Unit, string>.Success(Unit.Value);
        }

        public Task<Result<AgentSkill, string>> GetSkillAsync(string skillId, CancellationToken ct = default)
        {
            var skill = _skills.FirstOrDefault(s => s.Id.Equals(skillId, StringComparison.OrdinalIgnoreCase)
                || s.Name.Equals(skillId, StringComparison.OrdinalIgnoreCase));
            return skill is not null
                ? Task.FromResult(Result<AgentSkill, string>.Success(skill))
                : Task.FromResult(Result<AgentSkill, string>.Failure($"Skill '{skillId}' not found"));
        }

        public AgentSkill? GetSkill(string skillId) =>
            _skills.FirstOrDefault(s => s.Id.Equals(skillId, StringComparison.OrdinalIgnoreCase)
                || s.Name.Equals(skillId, StringComparison.OrdinalIgnoreCase));

        public Task<Result<IReadOnlyList<AgentSkill>, string>> FindSkillsAsync(
            string? category = null, IReadOnlyList<string>? tags = null, CancellationToken ct = default)
        {
            IEnumerable<AgentSkill> results = _skills;
            if (category is not null)
                results = results.Where(s => s.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
            if (tags is { Count: > 0 })
                results = results.Where(s => tags.Any(t => s.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)));
            return Task.FromResult(Result<IReadOnlyList<AgentSkill>, string>.Success(results.ToList().AsReadOnly()));
        }

        public Task<List<Skill>> FindMatchingSkillsAsync(
            string goal, Dictionary<string, object>? context = null, CancellationToken ct = default) =>
            Task.FromResult(_skills
                .Where(s => s.Name.Contains(goal, StringComparison.OrdinalIgnoreCase)
                    || s.Description.Contains(goal, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.ToSkill())
                .ToList());

        public Task<Result<Unit, string>> UpdateSkillAsync(AgentSkill skill, CancellationToken ct = default)
        {
            var idx = _skills.FindIndex(s => s.Id == skill.Id);
            if (idx < 0)
                return Task.FromResult(Result<Unit, string>.Failure($"Skill '{skill.Id}' not found"));
            _skills[idx] = skill;
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }

        public Task<Result<Unit, string>> RecordExecutionAsync(
            string skillId, bool success, long executionTimeMs, CancellationToken ct = default)
        {
            // No-op for simple registry
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }

        public void RecordSkillExecution(string skillId, bool success, long executionTimeMs)
        {
            // No-op for simple registry
        }

        public Task<Result<Unit, string>> UnregisterSkillAsync(string skillId, CancellationToken ct = default)
        {
            var removed = _skills.RemoveAll(s => s.Id.Equals(skillId, StringComparison.OrdinalIgnoreCase));
            return removed > 0
                ? Task.FromResult(Result<Unit, string>.Success(Unit.Value))
                : Task.FromResult(Result<Unit, string>.Failure($"Skill '{skillId}' not found"));
        }

        public Task<Result<IReadOnlyList<AgentSkill>, string>> GetAllSkillsAsync(CancellationToken ct = default) =>
            Task.FromResult(Result<IReadOnlyList<AgentSkill>, string>.Success((IReadOnlyList<AgentSkill>)_skills.AsReadOnly()));

        public IReadOnlyList<AgentSkill> GetAllSkills() => _skills.AsReadOnly();

        public Task<Result<Skill, string>> ExtractSkillAsync(
            PlanExecutionResult execution, string skillName, string description, CancellationToken ct = default) =>
            Task.FromResult(Result<Skill, string>.Failure("Not supported in simple registry"));
    }

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

    /// <summary>
    /// Try to match natural language patterns to pipeline tokens.
    /// </summary>
    private async Task<string?> TryNaturalLanguageTokenAsync(
        string input,
        string personaName,
        CancellationToken ct)
    {
        if (_allTokens == null) return null;

        var lower = input.ToLowerInvariant();

        // Common natural language patterns mapped to tokens
        var patterns = new (string Pattern, string Token, int ArgGroup)[]
        {
            // ArXiv/Papers
            (@"search\s+(?:arxiv|papers?|research)\s+(?:for\s+)?(.+)", "ArxivSearch", 1),
            (@"find\s+(?:papers?|research)\s+(?:on|about)\s+(.+)", "ArxivSearch", 1),
            (@"(?:arxiv|papers?)\s+(?:on|about|for)\s+(.+)", "ArxivSearch", 1),
            (@"research\s+(.+)\s+papers?", "ArxivSearch", 1),

            // Wikipedia
            (@"search\s+wiki(?:pedia)?\s+(?:for\s+)?(.+)", "WikiSearch", 1),
            (@"(?:look\s+up|lookup)\s+(.+)\s+(?:on\s+)?wiki(?:pedia)?", "WikiSearch", 1),
            (@"what\s+(?:is|are)\s+(.+)\s+(?:according\s+to\s+)?wiki(?:pedia)?", "WikiSearch", 1),
            (@"wiki(?:pedia)?\s+(.+)", "WikiSearch", 1),

            // Semantic Scholar
            (@"search\s+semantic\s+scholar\s+(?:for\s+)?(.+)", "SemanticScholarSearch", 1),
            (@"find\s+citations?\s+(?:for|about)\s+(.+)", "SemanticScholarSearch", 1),

            // Web fetch
            (@"fetch\s+(?:url\s+)?(.+)", "Fetch", 1),
            (@"get\s+(?:content\s+from|page)\s+(.+)", "Fetch", 1),
            (@"download\s+(.+)", "Fetch", 1),

            // Generate/LLM
            (@"generate\s+(?:text\s+)?(?:about|on|for)\s+(.+)", "Generate", 1),
            (@"write\s+(?:about|on)\s+(.+)", "Generate", 1),

            // Summarize
            (@"summarize\s+(.+)", "Summarize", 1),
            (@"give\s+(?:me\s+)?(?:a\s+)?summary\s+(?:of\s+)?(.+)", "Summarize", 1),

            // Skill execution
            (@"use\s+skill\s+(.+)", "UseSkill", 1),
            (@"apply\s+skill\s+(.+)", "UseSkill", 1),
        };

        foreach (var (pattern, token, argGroup) in patterns)
        {
            var match = Regex.Match(lower, pattern, RegexOptions.IgnoreCase);
            if (match.Success && _allTokens.ContainsKey(token))
            {
                var arg = match.Groups[argGroup].Value.Trim();
                var command = $"{token} '{arg}'";
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim($"[~] Interpreted as: {command}")}");
                return await TryExecuteSingleTokenAsync(command, personaName, ct);
            }
        }

        return null;
    }

    private async Task<string> HandleListSkillsAsync(string personaName)
    {
        if (_skillRegistry == null)
            return "I don't have any skills loaded right now.";

        var skills = _skillRegistry.GetAllSkills().ToList();
        if (skills.Count == 0)
            return "I haven't learned any skills yet. Say 'learn about' something to teach me.";

        AnsiConsole.WriteLine();
        var skillsTable = OuroborosTheme.ThemedTable("Skill", "Success Rate");
        foreach (var skill in skills.Take(10))
        {
            skillsTable.AddRow(Markup.Escape(skill.Name), Markup.Escape($"{skill.SuccessRate:P0}"));
        }
        if (skills.Count > 10)
            skillsTable.AddRow(Markup.Escape($"... and {skills.Count - 10} more"), "");
        AnsiConsole.Write(OuroborosTheme.ThemedPanel(skillsTable, $"My Skills ({skills.Count})"));

        return $"I know {skills.Count} skills. The top ones are: {string.Join(", ", skills.Take(5).Select(s => s.Name))}.";
    }

    private async Task<string> HandleUseToolAsync(string toolName, string toolInput, string personaName, CancellationToken ct)
    {
        if (_dynamicTools == null)
            return "I don't have any tools loaded right now.";

        var tool = _dynamicTools.Get(toolName);
        if (tool == null)
        {
            // Try to find a close match
            var availableTools = _dynamicTools.All.Select(t => t.Name).ToList();
            var closestMatch = availableTools
                .OrderBy(t => LevenshteinDistance(t.ToLower(), toolName.ToLower()))
                .FirstOrDefault();

            return $"I don't have a tool called '{toolName}'. Did you mean '{closestMatch}'?\n\nAvailable tools include: {string.Join(", ", availableTools.Take(10))}";
        }

        // If no input provided, show the tool's usage
        if (string.IsNullOrWhiteSpace(toolInput) || toolInput == "{}")
        {
            // For tools that don't need input, execute directly
            if (string.IsNullOrEmpty(tool.JsonSchema) || tool.JsonSchema == "null")
            {
                toolInput = "{}";
            }
            else
            {
                return $"**Tool: {tool.Name}**\n\n{tool.Description}\n\n**Required input format:**\n```json\n{tool.JsonSchema ?? "{}"}\n```\n\nExample: `tool {toolName} {{\"param\": \"value\"}}`";
            }
        }

        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.GoldText($"[Executing tool: {toolName}...]")}");

        try
        {
            var result = await tool.InvokeAsync(toolInput, ct);
            return result.Match(
                success => $"**{toolName} result:**\n\n{success}",
                error => $"**{toolName} failed:**\n\n{error}"
            );
        }
        catch (Exception ex)
        {
            return $"Tool execution error: {ex.Message}";
        }
    }

    private string HandleListTools(string personaName)
    {
        if (_dynamicTools == null)
            return "I don't have any tools loaded.";

        var tools = _dynamicTools.All.ToList();
        var sb = new StringBuilder();
        sb.AppendLine($"\n  **My Tools ({tools.Count} available)**\n");

        // Group by category
        var selfTools = tools.Where(t => t.Name.Contains("my_") || t.Name.Contains("self") || t.Name.Contains("rebuild")).ToList();
        var fileTools = tools.Where(t => t.Name.Contains("file") || t.Name.Contains("directory")).ToList();
        var systemTools = tools.Where(t => t.Name.Contains("process") || t.Name.Contains("system") || t.Name.Contains("powershell")).ToList();
        var otherTools = tools.Except(selfTools).Except(fileTools).Except(systemTools).ToList();

        if (selfTools.Any())
        {
            sb.AppendLine("  🧬 **Self-Modification:**");
            foreach (var t in selfTools.Take(8))
                sb.AppendLine($"    • `{t.Name}` - {Truncate(t.Description, 60)}");
            sb.AppendLine();
        }

        if (fileTools.Any())
        {
            sb.AppendLine("  📁 **File System:**");
            foreach (var t in fileTools.Take(6))
                sb.AppendLine($"    • `{t.Name}` - {Truncate(t.Description, 60)}");
            sb.AppendLine();
        }

        if (systemTools.Any())
        {
            sb.AppendLine("  💻 **System:**");
            foreach (var t in systemTools.Take(6))
                sb.AppendLine($"    • `{t.Name}` - {Truncate(t.Description, 60)}");
            sb.AppendLine();
        }

        if (otherTools.Any())
        {
            sb.AppendLine("  🔧 **Other:**");
            foreach (var t in otherTools.Take(8))
                sb.AppendLine($"    • `{t.Name}` - {Truncate(t.Description, 60)}");
        }

        sb.AppendLine("\n  **Usage:** `tool <name> {\"param\": \"value\"}`");

        AnsiConsole.MarkupLine(Markup.Escape(sb.ToString()));
        return $"I have {tools.Count} tools available. Key ones: {string.Join(", ", selfTools.Select(t => t.Name))}";
    }

    private string HandleSelfModificationHelp(string personaName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("\n  🧬 **Self-Modification Capabilities**\n");
        sb.AppendLine("  I can actually modify my own source code! Here's how:\n");
        sb.AppendLine("  1️⃣ **Search my code:**");
        sb.AppendLine("     `tool search_my_code {\"query\": \"what to find\"}`\n");
        sb.AppendLine("  2️⃣ **Read a file:**");
        sb.AppendLine("     `tool read_my_file {\"path\": \"src/Ouroboros.Cli/Commands/ImmersiveMode.cs\"}`\n");
        sb.AppendLine("  3️⃣ **Modify code:**");
        sb.AppendLine("     `tool modify_my_code {\"file\": \"path/to/file.cs\", \"search\": \"old text\", \"replace\": \"new text\"}`\n");
        sb.AppendLine("  4️⃣ **Create new tool:**");
        sb.AppendLine("     `tool create_new_tool {\"name\": \"my_tool\", \"description\": \"what it does\", \"implementation\": \"C# code\"}`\n");
        sb.AppendLine("  5️⃣ **Rebuild myself:**");
        sb.AppendLine("     `rebuild` or `tool rebuild_self`\n");
        sb.AppendLine("  6️⃣ **View/revert changes:**");
        sb.AppendLine("     `modification history` or `tool revert_modification {\"backup\": \"filename.backup\"}`");

        AnsiConsole.MarkupLine(Markup.Escape(sb.ToString()));
        return "Yes, I can modify myself! Use the commands above. Changes create automatic backups.";
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


    private string HandleListTokens(string personaName)
    {
        if (_allTokens == null || _allTokens.Count == 0)
            return "No pipeline tokens available.";

        AnsiConsole.WriteLine();
        var tokenTable = OuroborosTheme.ThemedTable("Token", "Description");
        foreach (var (name, info) in _allTokens.Take(15))
        {
            var desc = info.Description.Length > 40 ? info.Description[..40] : info.Description;
            tokenTable.AddRow(Markup.Escape(name), Markup.Escape(desc));
        }
        if (_allTokens.Count > 15)
            tokenTable.AddRow(Markup.Escape($"... and {_allTokens.Count - 15} more"), "");
        AnsiConsole.Write(OuroborosTheme.ThemedPanel(tokenTable, $"Pipeline Tokens ({_allTokens.Count})"));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Examples:")}");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.GoldText("ArxivSearch 'neural networks'")}           - Search papers");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.GoldText("WikiSearch 'quantum computing'")}          - Search Wikipedia");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.GoldText("ArxivSearch 'AI' | Summarize")}            - Chain with pipe");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.GoldText("Fetch 'https://example.com'")}             - Fetch web content");

        // Set context so follow-up questions get pipeline-aware responses
        _lastPipelineContext = "pipeline_tokens";

        return $"I have {_allTokens.Count} pipeline tokens available. Try commands like 'ArxivSearch neural networks' or chain them with pipes!";
    }

    private string HandlePipelineHelp(string personaName)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Pipeline Usage Guide"));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText("SINGLE COMMANDS:")}");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("ArxivSearch 'neural networks'")}     Search academic papers on arXiv");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("WikiSearch 'quantum computing'")}    Look up topics on Wikipedia");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("SemanticScholarSearch 'AI'")}        Search Semantic Scholar");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Fetch 'https://example.com'")}       Fetch content from any URL");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Generate 'topic'")}                  Generate text about a topic");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Summarize")}                         Summarize the last output");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText("CHAINED PIPELINES (use | to chain):")}");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("ArxivSearch 'transformers' | Summarize")}");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("WikiSearch 'machine learning' | Generate 'explanation'")}");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Fetch 'url' | UseOutput")}");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText("NATURAL LANGUAGE (I understand these too):")}");
        AnsiConsole.MarkupLine($"    'search arxiv for neural networks'");
        AnsiConsole.MarkupLine($"    'look up AI on wikipedia'");
        AnsiConsole.MarkupLine($"    'find papers about transformers'");
        AnsiConsole.MarkupLine($"    'summarize that'");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText("TIPS:")}");
        AnsiConsole.MarkupLine($"    - Use quotes around multi-word arguments");
        AnsiConsole.MarkupLine($"    - Chain multiple steps with the pipe | symbol");
        AnsiConsole.MarkupLine($"    - Say 'tokens' to see all available pipeline tokens");

        _lastPipelineContext = "pipeline_help";
        return "I can execute pipeline commands! Try 'ArxivSearch neural networks' or chain them like 'WikiSearch AI | Summarize'.";
    }

    private string HandleToolStats(string personaName)
    {
        if (_toolLearner == null)
            return "Tool learning is not available in this session.";

        var stats = _toolLearner.GetStats();
        AnsiConsole.WriteLine();
        var statsTable = OuroborosTheme.ThemedTable("Metric", "Value");
        statsTable.AddRow(Markup.Escape("Total patterns"), Markup.Escape($"{stats.TotalPatterns}"));
        statsTable.AddRow(Markup.Escape("Success rate"), Markup.Escape($"{stats.AvgSuccessRate:P0}"));
        statsTable.AddRow(Markup.Escape("Total usage"), Markup.Escape($"{stats.TotalUsage}"));
        AnsiConsole.Write(OuroborosTheme.ThemedPanel(statsTable, "Tool Learning Stats"));

        return $"I've learned {stats.TotalPatterns} patterns with a {stats.AvgSuccessRate:P0} success rate. Total usage: {stats.TotalUsage}.";
    }

    private async Task<string> HandleConnectionsAsync(string personaName, CancellationToken ct)
    {
        if (_interconnectedLearner == null)
            return "Interconnected learning is not available in this session.";

        AnsiConsole.WriteLine();

        // Show stats from the learner
        var stats = _interconnectedLearner.GetStats();
        int totalExecutions = stats.TotalToolExecutions + stats.TotalSkillExecutions + stats.TotalPipelineExecutions;
        double successRate = totalExecutions > 0 ? (double)stats.SuccessfulExecutions / totalExecutions : 0;
        var connTable = OuroborosTheme.ThemedTable("Metric", "Value");
        connTable.AddRow(Markup.Escape("Patterns Learned"), Markup.Escape($"{stats.LearnedPatterns}"));
        connTable.AddRow(Markup.Escape("Concepts Mapped"), Markup.Escape($"{stats.ConceptGraphNodes}"));
        connTable.AddRow(Markup.Escape("Executions Recorded"), Markup.Escape($"{totalExecutions}"));
        connTable.AddRow(Markup.Escape("Avg Success Rate"), Markup.Escape($"{successRate:P0}"));
        AnsiConsole.Write(OuroborosTheme.ThemedPanel(connTable, "Interconnected Learning"));

        // Show sample suggestions
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Accent("Sample suggestions for common goals:")}");
        var sampleGoals = new[] { "search", "analyze", "summarize" };
        foreach (var goal in sampleGoals)
        {
            var suggestion = await _interconnectedLearner.SuggestForGoalAsync(goal, _dynamicTools, ct);
            var actions = suggestion.MeTTaSuggestions.Concat(suggestion.RelatedConcepts).Take(3).ToList();
            if (actions.Count > 0)
            {
                AnsiConsole.MarkupLine($"    {OuroborosTheme.GoldText(goal)} -> \\[{Markup.Escape(string.Join(", ", actions))}]");
            }
        }

        return stats.LearnedPatterns > 0
            ? $"I have {stats.LearnedPatterns} learned patterns across {stats.ConceptGraphNodes} concepts. Use tools and skills to build more connections!"
            : "I haven't learned any patterns yet. Use skills and tools and I'll start learning relationships.";
    }

    private async Task<string> HandleGoogleSearchAsync(
        string query,
        string personaName,
        CancellationToken ct)
    {
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Accent($"[~] Searching Google for: {query}...")}");

        // Find the Google search tool
        var googleTool = _dynamicTools.All
            .FirstOrDefault(t => t.Name.Contains("google", StringComparison.OrdinalIgnoreCase) ||
                                 t.Name.Contains("search", StringComparison.OrdinalIgnoreCase));

        if (googleTool == null)
        {
            return "Google search tool is not available. Try 'add tool search' first.";
        }

        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await googleTool.InvokeAsync(query);
            stopwatch.Stop();

            AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok("[OK] Search complete")}");

            // Parse and display results
            var output = result.IsSuccess ? result.Value : "No results found.";
            if (output.Length > 500)
            {
                output = output[..500] + "...";
            }
            AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Accent("Results:")}");
            AnsiConsole.MarkupLine($"  {Markup.Escape(output.Replace("\n", "\n  "))}");

            // Learn from the search (interconnected learning)
            if (_interconnectedLearner != null)
            {
                await _interconnectedLearner.RecordToolExecutionAsync(
                    googleTool.Name,
                    query,
                    output,
                    true,
                    stopwatch.Elapsed,
                    ct);
            }

            return $"I found results for '{query}'. The search returned information about it.";
        }
        catch (Exception ex)
        {
            var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} \\[!] Search failed: {Markup.Escape(ex.Message)}[/]");
            return $"I couldn't complete the search. Error: {ex.Message}";
        }
    }

    private async Task<string> HandleRunSkillAsync(
        string skillName,
        string personaName,
        IVoiceOptions options,
        CancellationToken ct)
    {
        if (_skillRegistry == null)
            return "Skills are not available.";

        var skill = _skillRegistry.GetAllSkills()
            .FirstOrDefault(s => s.Name.Contains(skillName, StringComparison.OrdinalIgnoreCase));

        if (skill == null)
            return $"I don't know a skill called '{skillName}'. Say 'list skills' to see what I know.";

        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.GoldText($"[>] Executing skill: {skill.Name}")}");
        var results = new List<string>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        foreach (var step in skill.ToSkill().Steps)
        {
            AnsiConsole.MarkupLine($"      {OuroborosTheme.Accent("->")} {Markup.Escape(step.Action)}: {Markup.Escape(step.ExpectedOutcome)}");
            results.Add($"Step: {step.Action}");
            await Task.Delay(200, ct); // Simulate step execution
        }
        stopwatch.Stop();
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok("[OK] Skill complete")}");

        // Learn from skill execution (interconnected learning)
        if (_interconnectedLearner != null)
        {
            await _interconnectedLearner.RecordSkillExecutionAsync(
                skill.Name,
                string.Join(", ", skill.ToSkill().Steps.Select(s => s.Action)),
                string.Join("\n", results),
                true,
                ct);
        }

        return $"I ran the {skill.Name} skill. It has {skill.ToSkill().Steps.Count} steps.";
    }

    private async Task<string> HandleLearnAboutAsync(
        string topic,
        string personaName,
        IVoiceOptions options,
        CancellationToken ct)
    {
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.GoldText($"[~] Researching: {topic}...")}");

        // Use ArxivSearch if available
        if (_allTokens?.ContainsKey("ArxivSearch") == true)
        {
            // Simulate research
            await Task.Delay(500, ct);
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] Found research on {topic}")}");
        }

        // Create a simple skill from the topic
        if (_skillRegistry != null)
        {
            var stepParams = new Dictionary<string, object> { ["query"] = topic };
            var skill = new Skill(
                $"Research_{topic.Replace(" ", "_")}",
                $"Research skill for {topic}",
                new List<string>(),
                [new PlanStep($"Search for {topic}", stepParams, "research_results", 0.8)],
                0.75,
                0,
                DateTime.UtcNow,
                DateTime.UtcNow);
            await _skillRegistry.RegisterSkillAsync(skill.ToAgentSkill());
            return $"I learned about {topic} and created a research skill for it.";
        }

        return $"I researched {topic}. Interesting stuff!";
    }

    private async Task<string> HandleAddToolAsync(
        string toolName,
        string personaName,
        CancellationToken ct)
    {
        if (_dynamicToolFactory == null)
            return "Tool creation is not available.";

        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Accent($"[~] Creating tool: {toolName}...")}");

        try
        {
            // Create tool based on name hints for known patterns
            ITool? newTool = toolName.ToLowerInvariant() switch
            {
                var n when n.Contains("search") || n.Contains("google") || n.Contains("web") =>
                    _dynamicToolFactory.CreateWebSearchTool("duckduckgo"),
                var n when n.Contains("fetch") || n.Contains("url") || n.Contains("http") =>
                    _dynamicToolFactory.CreateUrlFetchTool(),
                var n when n.Contains("calc") || n.Contains("math") =>
                    _dynamicToolFactory.CreateCalculatorTool(),
                _ => null // Unknown type - will try LLM generation
            };

            if (newTool != null)
            {
                _dynamicTools = _dynamicTools.WithTool(newTool);
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] Created tool: {newTool.Name}")}");
                return $"I created a new {newTool.Name} tool. It's ready to use.";
            }

            // Unknown tool type - use LLM to generate it
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim("[~] Using AI to generate custom tool...")}");
            var description = $"A tool named {toolName} that performs operations related to {toolName}";
            var createResult = await _dynamicToolFactory.CreateToolAsync(toolName, description, ct);

            if (createResult.IsSuccess)
            {
                _dynamicTools = _dynamicTools.WithTool(createResult.Value);
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] Created custom tool: {createResult.Value.Name}")}");
                return $"I created a custom '{createResult.Value.Name}' tool using AI. It's ready to use.";
            }
            else
            {
                AnsiConsole.MarkupLine($"  [red]\\[!] AI tool generation failed: {Markup.Escape(createResult.Error)}[/]");
                return $"I couldn't create a '{toolName}' tool. Error: {createResult.Error}";
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"[!] Tool creation failed: {ex.Message}")}[/]");
        }

        return $"I had trouble creating that tool. Try being more specific about what it should do.";
    }

    private async Task<string> HandleCreateToolFromDescriptionAsync(
        string description,
        string personaName,
        CancellationToken ct)
    {
        if (_dynamicToolFactory == null)
            return "Tool creation is not available.";

        AnsiConsole.MarkupLine($"\n  [rgb(128,0,180)]\\[~] Creating custom tool from description...[/]");
        AnsiConsole.MarkupLine($"      {OuroborosTheme.Accent("Description:")} {Markup.Escape(description)}");

        try
        {
            // Generate a tool name from the description
            var words = description.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3)
                .Take(3)
                .Select(w => char.ToUpper(w[0]) + w[1..].ToLower());
            var toolName = string.Join("", words) + "Tool";
            if (toolName.Length < 6) toolName = "CustomTool";

            var createResult = await _dynamicToolFactory.CreateToolAsync(toolName, description, ct);

            if (createResult.IsSuccess)
            {
                _dynamicTools = _dynamicTools.WithTool(createResult.Value);
                AnsiConsole.MarkupLine(OuroborosTheme.Ok($"  [OK] Created tool: {createResult.Value.Name}"));
                return $"Done! I created a '{createResult.Value.Name}' tool that {description}. It's ready to use.";
            }
            else
            {
                AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"[!] Tool creation failed: {createResult.Error}")}[/]");
                return $"I couldn't create that tool. Error: {createResult.Error}";
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"[!] Tool creation failed: {ex.Message}")}[/]");
            return $"Tool creation failed: {ex.Message}";
        }
    }

    private async Task<string> HandleCreateToolFromContextAsync(
        string topic,
        string description,
        string personaName,
        CancellationToken ct)
    {
        if (_dynamicToolFactory == null)
            return "Tool creation is not available.";

        AnsiConsole.MarkupLine($"\n  [rgb(128,0,180)]\\[~] Creating tool based on our conversation...[/]");
        AnsiConsole.MarkupLine($"      {OuroborosTheme.Accent("Topic:")} {Markup.Escape(topic)}");

        try
        {
            var toolName = topic.Replace(" ", "") + "Tool";
            var createResult = await _dynamicToolFactory.CreateToolAsync(toolName, description, ct);

            if (createResult.IsSuccess)
            {
                _dynamicTools = _dynamicTools.WithTool(createResult.Value);
                AnsiConsole.MarkupLine(OuroborosTheme.Ok($"  [OK] Created tool: {createResult.Value.Name}"));
                return $"Done! I created '{createResult.Value.Name}'. It's ready to use.";
            }
            else
            {
                AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"[!] Tool creation failed: {createResult.Error}")}[/]");
                return $"I couldn't create that tool. {createResult.Error}";
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"[!] Tool creation failed: {ex.Message}")}[/]");
            return $"Tool creation failed: {ex.Message}";
        }
    }

    private async Task<string> HandleSmartToolAsync(
        string goal,
        string personaName,
        CancellationToken ct)
    {
        if (_toolLearner == null)
            return "Intelligent tool discovery is not available.";

        AnsiConsole.MarkupLine($"\n  [rgb(128,0,180)]\\[~] Finding best tool for: {Markup.Escape(goal)}...[/]");

        try
        {
            var result = await _toolLearner.FindOrCreateToolAsync(goal, _dynamicTools, ct);
            if (result.IsSuccess)
            {
                var (tool, wasCreated) = result.Value;
                AnsiConsole.MarkupLine(OuroborosTheme.Ok($"  [OK] {(wasCreated ? "Created" : "Found")} tool: {tool.Name}"));

                // Learn from tool usage (interconnected learning)
                if (_interconnectedLearner != null)
                {
                    await _interconnectedLearner.RecordToolExecutionAsync(
                        tool.Name,
                        goal,
                        $"Tool found for: {goal}",
                        true,
                        TimeSpan.Zero,
                        ct);
                }

                return $"I found the best tool for that: {tool.Name}.";
            }
            return $"I couldn't find a suitable tool for '{goal}'. {result.Error}";
        }
        catch (Exception ex)
        {
            return $"Smart tool search failed: {ex.Message}";
        }
    }

    private async Task<string> HandleMemoryRecallAsync(string topic, string personaName, CancellationToken ct)
    {
        if (_conversationMemory == null)
        {
            return "Conversation memory is not initialized.";
        }

        AnsiConsole.MarkupLine($"\n  [rgb(148,103,189)]\\[~] Searching memories for: {Markup.Escape(topic)}...[/]");

        try
        {
            var recall = await _conversationMemory.RecallAboutAsync(topic, ct);
            return recall;
        }
        catch (Exception ex)
        {
            return $"Memory search failed: {ex.Message}";
        }
    }

    private Task<string> HandleMemoryStatsAsync(string personaName, CancellationToken ct)
    {
        if (_conversationMemory == null)
        {
            return Task.FromResult("Conversation memory is not initialized.");
        }

        var stats = _conversationMemory.GetStats();
        var sb = new StringBuilder();
        sb.AppendLine("📝 **Conversation Memory Statistics**\n");
        sb.AppendLine($"  Total sessions: {stats.TotalSessions}");
        sb.AppendLine($"  Total conversation turns: {stats.TotalTurns}");
        sb.AppendLine($"  Current session turns: {stats.CurrentSessionTurns}");

        if (stats.OldestMemory.HasValue)
        {
            sb.AppendLine($"  Oldest memory: {stats.OldestMemory.Value:g}");
        }

        if (stats.CurrentSessionStart.HasValue)
        {
            sb.AppendLine($"  Current session started: {stats.CurrentSessionStart.Value:g}");
        }

        // Show recent sessions summary
        if (_conversationMemory.RecentSessions.Count > 0)
        {
            sb.AppendLine("\n  Recent sessions:");
            foreach (var session in _conversationMemory.RecentSessions.TakeLast(3))
            {
                sb.AppendLine($"    • {session.StartedAt:g}: {session.Turns.Count} turns");
            }
        }

        return Task.FromResult(sb.ToString());
    }

    private string HandleMindState()
    {
        if (_autonomousMind == null)
        {
            return "Autonomous mind is not initialized.";
        }

        return _autonomousMind.GetMindState();
    }

    private string HandleShowInterests()
    {
        if (_autonomousMind == null)
        {
            return "Autonomous mind is not initialized.";
        }

        var facts = _autonomousMind.LearnedFacts;
        var sb = new StringBuilder();
        sb.AppendLine("🎯 **My Current Interests & Discoveries**\n");

        if (facts.Count == 0)
        {
            sb.AppendLine("I haven't discovered anything yet. Let me explore the internet!");
            sb.AppendLine("\n💡 Try: `think about AI` or `add interest quantum computing`");
        }
        else
        {
            sb.AppendLine("**Recent Discoveries:**");
            foreach (var fact in facts.TakeLast(10))
            {
                sb.AppendLine($"  💡 {fact}");
            }
        }

        return sb.ToString();
    }

    private async Task<string> HandleFullReindexAsync(string personaName, CancellationToken ct)
    {
        if (_selfIndexer == null)
        {
            return "Self-indexer is not available. Qdrant may not be connected.";
        }

        AnsiConsole.MarkupLine($"\n  [rgb(148,103,189)]\\[~] Starting full workspace reindex...[/]");

        var progress = new Progress<IndexingProgress>(p =>
        {
            if (p.ProcessedFiles % 10 == 0 && p.ProcessedFiles > 0)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Dim($"      [{p.ProcessedFiles}/{p.TotalFiles}] {Markup.Escape(p.CurrentFile ?? "")}"));
            }
        });

        try
        {
            var result = await _selfIndexer.FullReindexAsync(clearExisting: true, progress, ct);
            return $"Full reindex complete! Processed {result.ProcessedFiles} files, indexed {result.IndexedChunks} chunks in {result.Elapsed.TotalSeconds:F1}s. ({result.SkippedFiles} skipped, {result.ErrorFiles} errors)";
        }
        catch (Exception ex)
        {
            return $"Reindex failed: {ex.Message}";
        }
    }

    private async Task<string> HandleIncrementalReindexAsync(string personaName, CancellationToken ct)
    {
        if (_selfIndexer == null)
        {
            return "Self-indexer is not available. Qdrant may not be connected.";
        }

        AnsiConsole.MarkupLine($"\n  [rgb(148,103,189)]\\[~] Starting incremental reindex (changed files only)...[/]");

        var progress = new Progress<IndexingProgress>(p =>
        {
            if (!string.IsNullOrEmpty(p.CurrentFile))
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Dim($"      [{p.ProcessedFiles}/{p.TotalFiles}] {Markup.Escape(Path.GetFileName(p.CurrentFile) ?? "")}"));
            }
        });

        try
        {
            var result = await _selfIndexer.IncrementalIndexAsync(progress, ct);
            if (result.TotalFiles == 0)
            {
                return "No files have changed since last index. Workspace is up to date!";
            }
            return $"Incremental reindex complete! Updated {result.ProcessedFiles} files, indexed {result.IndexedChunks} chunks in {result.Elapsed.TotalSeconds:F1}s.";
        }
        catch (Exception ex)
        {
            return $"Incremental reindex failed: {ex.Message}";
        }
    }

    private async Task<string> HandleIndexSearchAsync(string query, string personaName, CancellationToken ct)
    {
        if (_selfIndexer == null)
        {
            return "Self-indexer is not available. Qdrant may not be connected.";
        }

        AnsiConsole.MarkupLine($"\n  [rgb(148,103,189)]\\[~] Searching indexed workspace for: {Markup.Escape(query)}[/]");

        try
        {
            var results = await _selfIndexer.SearchAsync(query, limit: 5, scoreThreshold: 0.3f, ct);

            if (results.Count == 0)
            {
                return "No matching content found in the indexed workspace.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Found {results.Count} relevant matches:\n");

            foreach (var result in results)
            {
                var relPath = Path.GetRelativePath(Environment.CurrentDirectory, result.FilePath);
                sb.AppendLine($"📄 **{relPath}** (chunk {result.ChunkIndex + 1}, score: {result.Score:F2})");
                sb.AppendLine($"   {result.Content.Substring(0, Math.Min(200, result.Content.Length))}...\n");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Index search failed: {ex.Message}";
        }
    }

    private async Task<string> HandleIndexStatsAsync(string personaName, CancellationToken ct)
    {
        if (_selfIndexer == null)
        {
            return "Self-indexer is not available. Qdrant may not be connected.";
        }

        try
        {
            var stats = await _selfIndexer.GetStatsAsync(ct);
            return $"📊 **Index Statistics**\n" +
                   $"  Collection: {stats.CollectionName}\n" +
                   $"  Indexed files: {stats.IndexedFiles}\n" +
                   $"  Total vectors: {stats.TotalVectors}\n" +
                   $"  Vector dimensions: {stats.VectorSize}";
        }
        catch (Exception ex)
        {
            return $"Failed to get index stats: {ex.Message}";
        }
    }

    private async Task<string> HandleEmergenceAsync(
        string topic,
        string personaName,
        IVoiceOptions options,
        CancellationToken ct)
    {
        AnsiConsole.MarkupLine($"\n  [rgb(148,103,189)]\\[~] Running Ouroboros emergence cycle on: {Markup.Escape(topic)}...[/]");
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("      Phase 1: Research gathering..."));
        await Task.Delay(300, ct);
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("      Phase 2: Pattern extraction..."));
        await Task.Delay(300, ct);
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("      Phase 3: Synthesis..."));
        await Task.Delay(300, ct);
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("      Phase 4: Emergence detection..."));
        await Task.Delay(300, ct);
        AnsiConsole.MarkupLine(OuroborosTheme.Ok($"  [OK] Emergence cycle complete for {topic}"));

        return $"I completed an emergence cycle on {topic}. I've synthesized new patterns from the research.";
    }

    private async Task<string> HandlePipelineAsync(
        string pipeline,
        string personaName,
        IVoiceOptions options,
        CancellationToken ct)
    {
        if (_allTokens == null || _pipelineState == null)
            return "Pipeline execution is not available.";

        AnsiConsole.MarkupLine(OuroborosTheme.Warn($"\n  [>] Executing pipeline: {pipeline}"));

        try
        {
            // Split pipeline into steps
            var steps = pipeline.Split('|').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
            var state = _pipelineState;

            foreach (var stepStr in steps)
            {
                // Parse step: "TokenName 'arg'" or "TokenName arg" or just "TokenName"
                var match = Regex.Match(stepStr, @"^(\w+)\s*(?:'([^']*)'|""([^""]*)""|(.*))?$");
                if (!match.Success)
                {
                    AnsiConsole.MarkupLine($"      [red]{Markup.Escape($"[!] Invalid step syntax: {stepStr}")}[/]");
                    continue;
                }

                string tokenName = match.Groups[1].Value;
                string arg = match.Groups[2].Success ? match.Groups[2].Value :
                             match.Groups[3].Success ? match.Groups[3].Value :
                             match.Groups[4].Value.Trim();

                // Find the token
                if (_allTokens.TryGetValue(tokenName, out var tokenInfo))
                {
                    AnsiConsole.MarkupLine(OuroborosTheme.Dim($"      -> {tokenName}" + (string.IsNullOrEmpty(arg) ? "" : $" '{arg}'")));

                    try
                    {
                        // Set the query/prompt in state for this step
                        if (!string.IsNullOrEmpty(arg))
                        {
                            state.Query = arg;
                            state.Prompt = arg;
                        }

                        // Invoke the pipeline step method
                        var stepMethod = tokenInfo.Method;
                        object?[]? methodArgs = string.IsNullOrEmpty(arg) ? null : new object[] { arg };
                        var stepInstance = stepMethod.Invoke(null, methodArgs);

                        if (stepInstance is Step<CliPipelineState, CliPipelineState> step)
                        {
                            state = await step(state);
                        }
                        else if (stepInstance is Func<CliPipelineState, Task<CliPipelineState>> asyncStep)
                        {
                            state = await asyncStep(state);
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"         [red]{Markup.Escape($"[!] Step returned unexpected type: {stepInstance?.GetType().Name ?? "null"}")}[/]");
                        }
                    }
                    catch (Exception ex)
                    {
                        var innerEx = ex.InnerException ?? ex;
                        AnsiConsole.MarkupLine($"         [red]{Markup.Escape($"[!] Step error: {innerEx.Message}")}[/]");
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine($"      [red]{Markup.Escape($"[!] Unknown token: {tokenName}")}[/]");
                    // Try to suggest similar tokens
                    var suggestions = _allTokens.Keys
                        .Where(k => k.Contains(tokenName, StringComparison.OrdinalIgnoreCase) ||
                                    tokenName.Contains(k, StringComparison.OrdinalIgnoreCase))
                        .Take(3);
                    if (suggestions.Any())
                    {
                        AnsiConsole.MarkupLine(OuroborosTheme.Dim($"         Did you mean: {string.Join(", ", suggestions)}?"));
                    }
                }
            }

            // Update the shared pipeline state with results
            _pipelineState = state;

            AnsiConsole.MarkupLine(OuroborosTheme.Ok("  [OK] Pipeline complete"));

            // Return meaningful output
            if (!string.IsNullOrEmpty(state.Output))
            {
                // Truncate for voice but show full in console
                var preview = state.Output.Length > 300 ? state.Output[..300] + "..." : state.Output;
                AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Accent("Pipeline Output:")}\n  {Markup.Escape(preview)}");
                return $"I ran your {steps.Count}-step pipeline. Here's what I found: {preview}";
            }

            return $"I ran your {steps.Count}-step pipeline successfully.";
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"[!] Pipeline error: {ex.Message}")}[/]");
            return $"Pipeline error: {ex.Message}";
        }
    }

    /// <summary>
    /// Execute a single pipeline token by name.
    /// </summary>
    private async Task<string?> TryExecuteSingleTokenAsync(
        string input,
        string personaName,
        CancellationToken ct)
    {
        if (_allTokens == null || _pipelineState == null)
            return null;

        // Parse: "TokenName arg" or "TokenName 'arg'"
        var match = Regex.Match(input.Trim(), @"^(\w+)\s*(?:'([^']*)'|""([^""]*)""|(.*))?$");
        if (!match.Success) return null;

        string tokenName = match.Groups[1].Value;
        string arg = match.Groups[2].Success ? match.Groups[2].Value :
                     match.Groups[3].Success ? match.Groups[3].Value :
                     match.Groups[4].Value.Trim();

        // Check if this is a known token
        if (!_allTokens.TryGetValue(tokenName, out var tokenInfo))
            return null;

        AnsiConsole.MarkupLine(OuroborosTheme.Warn($"\n  [>] Executing: {tokenName}" + (string.IsNullOrEmpty(arg) ? "" : $" '{arg}'")));

        try
        {
            var state = _pipelineState;
            if (!string.IsNullOrEmpty(arg))
            {
                state.Query = arg;
                state.Prompt = arg;
            }

            var stepMethod = tokenInfo.Method;
            object?[]? methodArgs = string.IsNullOrEmpty(arg) ? null : new object[] { arg };
            var stepInstance = stepMethod.Invoke(null, methodArgs);

            if (stepInstance is Step<CliPipelineState, CliPipelineState> step)
            {
                state = await step(state);
            }
            else if (stepInstance is Func<CliPipelineState, Task<CliPipelineState>> asyncStep)
            {
                state = await asyncStep(state);
            }

            _pipelineState = state;
            AnsiConsole.MarkupLine(OuroborosTheme.Ok($"  [OK] {tokenName} complete"));

            if (!string.IsNullOrEmpty(state.Output))
            {
                var preview = state.Output.Length > 300 ? state.Output[..300] + "..." : state.Output;
                return $"I executed {tokenName}. Result: {preview}";
            }

            return $"I executed {tokenName} successfully.";
        }
        catch (Exception ex)
        {
            var innerEx = ex.InnerException ?? ex;
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"[!] Error: {innerEx.Message}")}[/]");
            return $"Error executing {tokenName}: {innerEx.Message}";
        }
    }

    /// <summary>
    /// Learns from an interaction through the consciousness dream cycle.
    /// </summary>
    private async Task LearnFromInteractionAsync(
        string userInput,
        string response,
        CancellationToken ct)
    {
        if (_distinctionLearner == null || _dream == null)
        {
            return;
        }

        try
        {
            // Learn through dream cycle
            await foreach (var moment in _dream.WalkTheDream(userInput, ct))
            {
                var observation = new Observation(
                    Content: userInput,
                    Timestamp: DateTime.UtcNow,
                    PriorCertainty: _currentDistinctionState.EpistemicCertainty,
                    Context: new Dictionary<string, object>
                    {
                        ["response_length"] = response.Length,
                        ["stage"] = moment.Stage.ToString()
                    });

                var result = await _distinctionLearner.UpdateFromDistinctionAsync(
                    _currentDistinctionState,
                    observation,
                    moment.Stage.ToString(),
                    ct);

                if (result.IsSuccess)
                {
                    _currentDistinctionState = result.Value;
                }

                // At Recognition stage, apply self-insight
                if (moment.Stage == DreamStage.Recognition)
                {
                    var recognizeResult = await _distinctionLearner.RecognizeAsync(
                        _currentDistinctionState,
                        userInput,
                        ct);

                    if (recognizeResult.IsSuccess)
                    {
                        _currentDistinctionState = recognizeResult.Value;
                    }
                }
            }

            // Periodic dissolution (every 10 cycles)
            if (_currentDistinctionState.CycleCount % DistinctionLearningConstants.DissolutionCycleInterval == 0)
            {
                await _distinctionLearner.DissolveAsync(
                    _currentDistinctionState,
                    DissolutionStrategy.FitnessThreshold,
                    ct);
            }
        }
        catch (Exception ex)
        {
            // Log the error but don't disrupt the interaction
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"[!] Distinction learning error: {ex.Message}")}[/]");
        }
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

    // Causal extraction and graph building consolidated in SharedAgentBootstrap.
    // Call sites use SharedAgentBootstrap.TryExtractCausalTerms / BuildMinimalCausalGraph.
}
