// <copyright file="ImmersiveMode.RunAsync.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.CLI.Commands;

using System.Text;
using LangChain.DocumentLoaders;
using LangChain.Providers.Ollama;
using Ouroboros.Abstractions.Agent;
using Ouroboros.Application.Configuration;
using Ouroboros.Agent;
using Ouroboros.Agent.MetaAI;
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

public sealed partial class ImmersiveMode
{
    public async Task RunAsync(IVoiceOptions options, CancellationToken ct = default)
    {
        var personaName = options.Persona;
        var random = new Random();

        // Clear console safely (may fail in redirected/piped scenarios)
        try { Console.Clear(); } catch (IOException) { /* ignore — redirected console */ }
        PrintImmersiveBanner(personaName);

        // Initialize MeTTa engine for symbolic reasoning
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("  [~] Initializing consciousness systems..."));
        using var mettaEngine = new InMemoryMeTTaEngine();

        // Initialize embedding model for memory (via SharedAgentBootstrap)
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("  [~] Connecting to memory systems..."));
        var embeddingModel = Ouroboros.CLI.Services.SharedAgentBootstrap.CreateEmbeddingModel(
            options.Endpoint, options.EmbedModel,
            msg => AnsiConsole.MarkupLine(msg.Contains("unavailable")
                ? OuroborosTheme.Warn($"  [!] {msg}")
                : OuroborosTheme.Ok($"  [OK] {msg}")));

        // Create the immersive persona (or use the one from OuroborosAgent master control)
        ImmersivePersona? ownedPersona = null;
        ImmersivePersona persona;
        if (_configuredPersona != null)
        {
            persona = _configuredPersona;
            AnsiConsole.MarkupLine(OuroborosTheme.Ok("  [OK] Using Iaret persona from OuroborosAgent (master control)"));
        }
        else
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Dim("  [~] Awakening persona..."));
            var qdrantClient = _serviceProvider?.GetService<QdrantClient>();
            var collectionRegistry = _serviceProvider?.GetService<IQdrantCollectionRegistry>();
            if (qdrantClient != null)
            {
#pragma warning disable CS0618 // DI constructor preferred
                ownedPersona = new ImmersivePersona(
                    personaName,
                    mettaEngine,
                    embeddingModel,
                    qdrantClient,
                    collectionRegistry);
#pragma warning restore CS0618
            }
            else
            {
#pragma warning disable CS0618 // Obsolete endpoint-string constructor
                ownedPersona = new ImmersivePersona(
                    personaName,
                    mettaEngine,
                    embeddingModel,
                    options.QdrantEndpoint);
#pragma warning restore CS0618
            }
            persona = ownedPersona;
        }

        // AutonomousThought console output + avatar wiring is handled entirely by
        // ImmersiveSubsystem.WirePersonaEvents (called after mind init at line ~737).
        // Do NOT subscribe here — ImmersiveSubsystem is the single print point.

        persona.ConsciousnessShift += (_, e) =>
        {
            AnsiConsole.MarkupLine($"\n  [rgb(128,0,180)]{Markup.Escape($"[consciousness] Emotional shift: {e.NewEmotion} (Δ arousal: {e.ArousalChange:+0.00;-0.00})")}[/]");
        };

        // Awaken the persona
        await persona.AwakenAsync(ct);

        AnsiConsole.MarkupLine(OuroborosTheme.Ok($"\n  [OK] {personaName} is awake\n"));

        // Initialize skills system
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("  [~] Loading skills and tools..."));
        await InitializeSkillsAsync(options, embeddingModel, mettaEngine);

        // Initialize speech services
        var (ttsService, sttService, speechDetector) = await InitializeSpeechServicesAsync(options);

        // Display consciousness state
        PrintConsciousnessState(persona);

        // Speak wake-up phrase — use personalized greeting from personality engine
        var wakePhrase = persona.GetPersonalizedGreeting();
        AnsiConsole.MarkupLine(OuroborosTheme.Ok($"\n  {personaName}: {wakePhrase}"));

        if (ttsService != null)
        {
            await SpeakAsync(ttsService, wakePhrase, personaName);
        }

        // Initialize persistent conversation memory
        var qdrantEndpoint = NormalizeEndpoint(options.QdrantEndpoint, DefaultEndpoints.QdrantGrpc);
        {
            var client = _serviceProvider?.GetService<QdrantClient>();
            var registry = _serviceProvider?.GetService<IQdrantCollectionRegistry>();
            if (client != null && registry != null)
            {
                _conversationMemory = new PersistentConversationMemory(client, registry, embeddingModel);
            }
            else
            {
#pragma warning disable CS0618 // Obsolete endpoint-string constructor
                _conversationMemory = new PersistentConversationMemory(
                    embeddingModel,
                    new ConversationMemoryConfig { QdrantEndpoint = qdrantEndpoint });
#pragma warning restore CS0618
            }
        }
        await _conversationMemory.InitializeAsync(personaName, ct);
        var memStats = _conversationMemory.GetStats();
        if (memStats.TotalSessions > 0)
        {
            AnsiConsole.MarkupLine($"[rgb(148,103,189)]{Markup.Escape($"  [Memory] Loaded {memStats.TotalSessions} previous conversations ({memStats.TotalTurns} turns)")}[/]");
        }

        // Initialize persistent network state projector for learning persistence
        if (embeddingModel != null)
        {
            try
            {
                var dag = new MerkleDag();
                var nsClient = _serviceProvider?.GetService<QdrantClient>();
                var nsRegistry = _serviceProvider?.GetService<IQdrantCollectionRegistry>();
                var nsSettings = _serviceProvider?.GetService<QdrantSettings>();
                if (nsClient != null && nsRegistry != null)
                {
                    _networkStateProjector = new PersistentNetworkStateProjector(
                        dag,
                        nsClient,
                        nsRegistry,
                        async text => await embeddingModel.CreateEmbeddingsAsync(text));
                }
                else
                {
                    _networkStateProjector = new PersistentNetworkStateProjector(
                        dag,
                        qdrantEndpoint,
                        async text => await embeddingModel.CreateEmbeddingsAsync(text));
                }
                await _networkStateProjector.InitializeAsync(ct);
                AnsiConsole.MarkupLine($"[rgb(148,103,189)]{Markup.Escape($"  [NetworkState] Epoch {_networkStateProjector.CurrentEpoch}, {_networkStateProjector.RecentLearnings.Count} learnings loaded")}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  [!] Network state persistence unavailable: {ex.Message}"));
            }
        }

        // Initialize distinction learning
        try
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Dim("  [~] Initializing distinction learning..."));
            var storageConfig = DistinctionStorageConfig.Default;
            var storage = new FileSystemDistinctionWeightStorage(storageConfig);
            _distinctionLearner = new DistinctionLearner(storage);
            _dream = new ConsciousnessDream();
            _currentDistinctionState = DistinctionState.Initial();
            AnsiConsole.MarkupLine($"[rgb(148,103,189)]{Markup.Escape("  [DistinctionLearning] Ready to learn from consciousness cycles")}[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  [!] Distinction learning unavailable: {ex.Message}"));
        }

        // Initialize self-persistence for mind state storage in Qdrant
        if (embeddingModel != null)
        {
            try
            {
                var spSettings = _serviceProvider?.GetService<QdrantSettings>();
                if (spSettings != null)
                {
                    _selfPersistence = new SelfPersistence(
                        spSettings,
                        async text => await embeddingModel.CreateEmbeddingsAsync(text));
                }
                else
                {
                    _selfPersistence = new SelfPersistence(
                        qdrantEndpoint,
                        async text => await embeddingModel.CreateEmbeddingsAsync(text));
                }
                await _selfPersistence.InitializeAsync(ct);
                AnsiConsole.MarkupLine($"[rgb(148,103,189)]{Markup.Escape("  [SelfPersistence] Qdrant collection 'ouroboros_self' ready for mind state storage")}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  [!] Self-persistence unavailable: {ex.Message}"));
            }
        }

        // Initialize autonomous mind for background thinking and curiosity
        _autonomousMind = new AutonomousMind();
        _autonomousMind.ThinkFunction = async (prompt, token) =>
        {
            // Use orchestration for autonomous thinking - routes to appropriate model
            return await GenerateWithOrchestrationAsync(prompt, useDivideAndConquer: false, token);
        };

        // Wire up pipeline-based reasoning function for monadic thinking
        _autonomousMind.PipelineThinkFunction = async (prompt, existingBranch, token) =>
        {
            // Use orchestration for pipeline-based thinking
            var response = await GenerateWithOrchestrationAsync(prompt, useDivideAndConquer: false, token);

            // If we have a branch, add the thought as a reasoning event
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

        // Wire up state persistence functions
        _autonomousMind.PersistLearningFunction = async (category, content, confidence, token) =>
        {
            if (_networkStateProjector != null)
            {
                await _networkStateProjector.RecordLearningAsync(
                    category,
                    content,
                    "autonomous_mind",
                    confidence,
                    token);
            }
        };

        _autonomousMind.PersistEmotionFunction = async (emotion, token) =>
        {
            if (_networkStateProjector != null)
            {
                await _networkStateProjector.RecordLearningAsync(
                    "emotional_state",
                    $"Emotion: {emotion.DominantEmotion} (arousal={emotion.Arousal:F2}, valence={emotion.Valence:F2}) - {emotion.Description}",
                    "autonomous_mind",
                    0.6,
                    token);
            }
        };

        _autonomousMind.SearchFunction = async (query, token) =>
        {
            var searchTool = _dynamicToolFactory?.CreateWebSearchTool("duckduckgo");
            if (searchTool != null)
            {
                var result = await searchTool.InvokeAsync(query, token);
                return result.Match(s => s, e => "");
            }
            return "";
        };
        _autonomousMind.ExecuteToolFunction = async (toolName, input, token) =>
        {
            var tool = _dynamicTools.Get(toolName);
            if (tool != null)
            {
                var result = await tool.InvokeAsync(input, token);
                return result.Match(s => s, e => $"Error: {e}");
            }
            return "Tool not found";
        };

        // Sanitize raw outputs through LLM for natural language
        _autonomousMind.SanitizeOutputFunction = async (rawOutput, token) =>
        {
            var model = _orchestratedModel ?? _baseModel;
            if (model == null || string.IsNullOrWhiteSpace(rawOutput))
                return rawOutput;

            try
            {
                string prompt = $@"Summarize this in ONE brief, natural sentence (max 50 words). No markdown:
{rawOutput}";

                string sanitized = await model.GenerateTextAsync(prompt, token);
                return string.IsNullOrWhiteSpace(sanitized) ? rawOutput : sanitized.Trim();
            }
            catch
            {
                return rawOutput;
            }
        };

        // Wire up limitation-busting tools with LLM functions
        VerifyClaimTool.SearchFunction = _autonomousMind.SearchFunction;
        VerifyClaimTool.EvaluateFunction = async (prompt, token) =>
        {
            var model = _orchestratedModel ?? _baseModel;
            return model != null ? await model.GenerateTextAsync(prompt, token) : "";
        };
        ReasoningChainTool.ReasonFunction = async (prompt, token) =>
        {
            var model = _orchestratedModel ?? _baseModel;
            return model != null ? await model.GenerateTextAsync(prompt, token) : "";
        };
        ParallelToolsTool.ExecuteToolFunction = _autonomousMind.ExecuteToolFunction;
        CompressContextTool.SummarizeFunction = async (prompt, token) =>
        {
            var model = _orchestratedModel ?? _baseModel;
            return model != null ? await model.GenerateTextAsync(prompt, token) : "";
        };
        SelfDoubtTool.CritiqueFunction = async (prompt, token) =>
        {
            var model = _orchestratedModel ?? _baseModel;
            return model != null ? await model.GenerateTextAsync(prompt, token) : "";
        };
        ParallelMeTTaThinkTool.OllamaFunction = async (prompt, token) =>
        {
            var model = _orchestratedModel ?? _baseModel;
            return model != null ? await model.GenerateTextAsync(prompt, token) : "";
        };
        OuroborosMeTTaTool.OllamaFunction = async (prompt, token) =>
        {
            var model = _orchestratedModel ?? _baseModel;
            return model != null ? await model.GenerateTextAsync(prompt, token) : "";
        };

        // Wire up autonomous mind events (console output only; avatar updates via ImmersiveSubsystem)
        _autonomousMind.OnProactiveMessage += (msg) =>
        {
            string savedInput;
            lock (_inputLock)
            {
                savedInput = _currentInputBuffer.ToString();
            }

            // Clear current line and show proactive message
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[rgb(128,0,180)]{Markup.Escape($"[Autonomous] {msg}")}[/]");

            // Restore prompt and any text user was typing
            AnsiConsole.Markup($"\n{OuroborosTheme.Warn(_currentPromptPrefix)}");
            if (!string.IsNullOrEmpty(savedInput))
            {
                AnsiConsole.Markup(Markup.Escape(savedInput));
            }
        };
        _autonomousMind.OnThought += (thought) =>
        {
            System.Diagnostics.Debug.WriteLine($"[Thought] {thought.Type}: {thought.Content}");
        };
        _autonomousMind.OnDiscovery += (query, fact) =>
        {
            System.Diagnostics.Debug.WriteLine($"[Discovery] {query}: {fact}");
        };
        _autonomousMind.OnEmotionalChange += (emotion) =>
        {
            AnsiConsole.MarkupLine($"\n  [rgb(148,103,189)]{Markup.Escape($"[mind] Emotional shift: {emotion.DominantEmotion} ({emotion.Description})")}[/]");
        };
        _autonomousMind.OnStatePersisted += (msg) =>
        {
            System.Diagnostics.Debug.WriteLine($"[State] {msg}");
        };

        // Start autonomous thinking
        _autonomousMind.Start();
        AnsiConsole.MarkupLine(OuroborosTheme.Ok("  [OK] Autonomous mind active (thinking, exploring, learning in background)"));
        AnsiConsole.MarkupLine(OuroborosTheme.Ok("       State persistence enabled (thoughts, emotions, learnings)"));

        // Wire up self-persistence tools to access the mind and persistence service
        if (_selfPersistence != null && _autonomousMind != null)
        {
            SystemAccessTools.SharedPersistence = _selfPersistence;
            SystemAccessTools.SharedMind = _autonomousMind;
            AnsiConsole.MarkupLine($"[rgb(148,103,189)]{Markup.Escape("  [Tools] Self-persistence tools linked: persist_self, restore_self, search_my_thoughts, persistence_stats")}[/]");
        }

        // Initialize ethics + cognitive physics + phi for response gating (via SharedAgentBootstrap)
        _immersiveEthics = Ouroboros.Core.Ethics.EthicsFrameworkFactory.CreateDefault();
        var (cogPhysicsEngine, cogState) = Ouroboros.CLI.Services.SharedAgentBootstrap.CreateCognitivePhysics();
        _immersiveCogPhysics = cogPhysicsEngine;
        _immersiveCogState = cogState;
        _immersivePhiCalc = new Ouroboros.Providers.IITPhiCalculator();
        _immersiveLastTopic = "general";
        AnsiConsole.MarkupLine($"[rgb(148,103,189)]{Markup.Escape("  [OK] Ethics gate + CognitivePhysics + Φ (IIT) online")}[/]");

        // Episodic memory + causal reasoning (resolved from DI — registered in RegisterEngineInterfaces)
        _episodicMemory = _serviceProvider?.GetService<Ouroboros.Pipeline.Memory.IEpisodicMemoryEngine>()
            ?? Ouroboros.CLI.Services.SharedAgentBootstrap.CreateEpisodicMemory(qdrantEndpoint, embeddingModel);
        _causalReasoning = _serviceProvider?.GetService<Ouroboros.Core.Reasoning.ICausalReasoningEngine>()
            ?? new Ouroboros.Core.Reasoning.CausalReasoningEngine();

        // Neural-symbolic bridge (via SharedAgentBootstrap)
        _neuralSymbolicBridge = Ouroboros.CLI.Services.SharedAgentBootstrap.CreateNeuralSymbolicBridge(
            _baseModel, mettaEngine);

        // Curiosity engine + sovereignty gate (via SharedAgentBootstrap)
        (_curiosityEngine, _sovereigntyGate) = Ouroboros.CLI.Services.SharedAgentBootstrap
            .CreateCuriosityAndSovereignty(
                _baseModel!,
                embeddingModel,
                mettaEngine,
                _autonomousMind,
                ct);

        AnsiConsole.MarkupLine($"[rgb(148,103,189)]{Markup.Escape("  [OK] EpisodicMemory + NeuralSymbolic + CuriosityEngine + SovereigntyGate online")}[/]");

        // Launch avatar and wire all avatar ↔ persona/mind events via ImmersiveSubsystem
        _immersive = new Subsystems.ImmersiveSubsystem();
        // When OuroborosAgent is wired, its EmbodimentSubsystem already owns the avatar —
        // skip standalone avatar startup to avoid double-bind on the same port/HttpListener.
        var avatarEnabled = !HasSubsystems && options switch {
            Ouroboros.Options.OuroborosOptions o => o.Avatar,
            Ouroboros.Options.ImmersiveCommandVoiceOptions i => i.Avatar,
            _ => false,
        };
        var avatarPort = options switch {
            Ouroboros.Options.OuroborosOptions o => o.AvatarPort,
            Ouroboros.Options.ImmersiveCommandVoiceOptions i => i.AvatarPort,
            _ => 0,
        };
        await _immersive.InitializeStandaloneAsync(personaName, avatarEnabled, avatarPort, ct);

        // When OuroborosAgent is wired, its EmbodimentSubsystem owns the avatar.
        // Inject it into ImmersiveSubsystem so presence-state animations (speaking/listening/idle)
        // and thought notifications reach the avatar instead of being silently dropped.
        if (_configuredAvatarService != null && _immersive.AvatarService == null)
        {
            _immersive.AvatarService = _configuredAvatarService;
            AnsiConsole.MarkupLine(OuroborosTheme.Ok("  [OK] Avatar wired from OuroborosAgent (speaking/mood animations active)"));
        }

        _immersive.WirePersonaEvents(persona, _autonomousMind);

        // If --room-mode is active, also run an ambient room listener alongside the interactive session
        var roomModeEnabled = options switch
        {
            Ouroboros.Options.OuroborosOptions o => o.RoomMode,
            Ouroboros.Options.ImmersiveCommandVoiceOptions o => o.RoomMode,
            _ => false
        };
        Ouroboros.CLI.Services.RoomPresence.AmbientRoomListener? roomListener = null;
        if (roomModeEnabled)
        {
            var roomStt = await RoomMode.InitializeSttForRoomAsync();
            if (roomStt != null)
            {
                roomListener = new Ouroboros.CLI.Services.RoomPresence.AmbientRoomListener(roomStt);
                roomListener.OnUtterance += (u) =>
                {
                    AnsiConsole.MarkupLine($"\n  [rgb(148,103,189)]{Markup.Escape($"[room] {u.Text}")}[/]");
                    _immersive?.PushTopicHint(u.Text);
                };
                await roomListener.StartAsync(ct);
                AnsiConsole.MarkupLine($"[rgb(148,103,189)]{Markup.Escape("  [OK] Room listener active (ambient mode alongside interactive session)")}[/]");
            }
        }

        // Main interaction loop - use persistent memory
        var conversationHistory = _conversationMemory.GetActiveHistory();
        var chatModel = await CreateChatModelAsync(options);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Get input (voice or text)
                _immersive?.SetPresenceState("Listening", "attentive");
                AnsiConsole.Markup($"\n{OuroborosTheme.Warn("  You: ")}");
                _currentPromptPrefix = "  You: ";

                string? input;
                if (sttService != null && speechDetector != null)
                {
                    // Claim the microphone — room listener yields until we're done.
                    Ouroboros.CLI.Services.RoomPresence.AmbientRoomListener.ImmersiveListeningActive = true;
                    try
                    {
                        input = await ListenWithVADAsync(sttService, speechDetector, ct);
                    }
                    finally
                    {
                        Ouroboros.CLI.Services.RoomPresence.AmbientRoomListener.ImmersiveListeningActive = false;
                    }
                    if (!string.IsNullOrEmpty(input))
                    {
                        AnsiConsole.MarkupLine(Markup.Escape(input));
                    }
                }
                else
                {
                    input = await ReadLinePreservingBufferAsync(ct);
                }

                if (string.IsNullOrWhiteSpace(input)) continue;

                // Check for exit commands
                if (IsExitCommand(input))
                {
                    var goodbye = await GenerateGoodbyeAsync(persona, chatModel);
                    AnsiConsole.MarkupLine(OuroborosTheme.Ok($"\n  {personaName}: {goodbye}"));

                    if (ttsService != null)
                    {
                        await SpeakAsync(ttsService, goodbye, personaName);
                    }

                    break;
                }

                // Check for introspection commands
                if (IsIntrospectionCommand(input))
                {
                    await HandleIntrospectionAsync(persona, input, ttsService, personaName);
                    continue;
                }

                // Check for replication commands
                if (IsReplicationCommand(input))
                {
                    await HandleReplicationAsync(persona, input, ttsService, personaName, ct);
                    continue;
                }

                // Check for skill/action commands
                var actionResult = await TryHandleActionAsync(input, persona, ttsService, personaName, options, ct);
                if (actionResult != null)
                {
                    // Action was handled - speak result if available
                    if (!string.IsNullOrEmpty(actionResult) && ttsService != null)
                    {
                        await SpeakAsync(ttsService, actionResult, personaName);
                    }
                    continue;
                }

                // Update conversation context for context-aware thoughts
                var toolNames = _dynamicTools?.All.Select(t => t.Name).ToList() ?? [];
                var skillNames = _skillRegistry?.GetAllSkills().Select(s => s.Name).ToList() ?? [];
                persona.UpdateInnerDialogContext(input, toolNames, skillNames);

                // Add user input to persistent memory
                var lastUserMsg = conversationHistory.LastOrDefault(h => h.Role == "user").Content;
                if (lastUserMsg != input)
                {
                    conversationHistory.Add(("user", input));
                    if (_conversationMemory != null)
                    {
                        await _conversationMemory.AddTurnAsync("user", input, ct);
                    }
                }

                // Push topic hint to avatar for stage positioning + expression
                _immersive?.PushTopicHint(input);

                // Process through the persona's consciousness
                _immersive?.SetPresenceState("Processing", "contemplative");
                AnsiConsole.MarkupLine(OuroborosTheme.Dim($"  [{GetDynamicThinkingPhrase(input, random)}]"));

                // Generate conscious response
                var response = await GenerateImmersiveResponseAsync(
                    persona,
                    chatModel,
                    input,
                    conversationHistory,
                    ct);

                // Record learnings from this interaction
                if (_networkStateProjector != null)
                {
                    await RecordInteractionLearningsAsync(input, response, persona, ct);
                }

                // Learn from distinction consciousness cycle (async, don't block)
                _ = LearnFromInteractionAsync(input, response, ct);

                // Add assistant response to persistent memory
                conversationHistory.Add(("assistant", response));
                if (_conversationMemory != null)
                {
                    await _conversationMemory.AddTurnAsync("assistant", response, ct);
                }

                // Keep local history manageable (persistent memory handles full history)
                if (conversationHistory.Count > 30)
                {
                    conversationHistory.RemoveRange(0, 2);
                }

                // Detect tool creation context from conversation
                DetectToolCreationContext(input, response);

                // Display response with emotional context
                _immersive?.SetPresenceState("Speaking", persona.Consciousness?.DominantEmotion ?? "warm");
                PrintResponse(persona, personaName, response);

                // Speak response — Iaret detects the RESPONSE language and passes it
                // directly to Azure TTS. The model may respond in a different language than
                // the user's input (e.g. user types "hi :)" but Iaret replies in German).
                if (ttsService != null)
                {
                    if (ttsService is Ouroboros.Providers.TextToSpeech.AzureNeuralTtsService azureDirect)
                    {
                        // Detect response language via LanguageSubsystem (Ollama LLM with heuristic fallback).
                        var responseLang  = await Subsystems.LanguageSubsystem
                            .DetectStaticAsync(response, CancellationToken.None)
                            .ConfigureAwait(false);
                        var targetCulture = responseLang.Culture;
                        // Keep _lastDetectedCulture in sync for OuroborosAgent's direct TTS path.
                        _lastDetectedCulture = targetCulture;

                        AnsiConsole.MarkupLine(OuroborosTheme.Dim($"  \\[tts: {Markup.Escape(responseLang.Language)} ({Markup.Escape(targetCulture)})]"));

                        // Pass culture explicitly — no state mutation, no synthesizer re-init.
                        await azureDirect.SpeakAsync(response, targetCulture, CancellationToken.None);
                    }
                    else
                    {
                        await SpeakAsync(ttsService, response, personaName);
                    }
                }

                _immersive?.SetPresenceState("Idle", persona.Consciousness?.DominantEmotion ?? "neutral");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                var errorFace = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
                AnsiConsole.MarkupLine($"\n  [red]{Markup.Escape(errorFace)} {Markup.Escape($"✗ [error] {ex.Message}")}[/]");
            }
        }

        // Final consciousness state
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("\n  [~] Consciousness fading..."));
        PrintConsciousnessState(persona);

        // Persist final network state and learnings
        if (_networkStateProjector != null)
        {
            try
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Dim("  [~] Persisting learnings..."));
                // Use CancellationToken.None — session token is already cancelled at this point
                // (Ctrl+C fired), but we still want the final snapshot to complete.
                await _networkStateProjector.ProjectAndPersistAsync(
                    System.Collections.Immutable.ImmutableDictionary<string, string>.Empty
                        .Add("event", "session_end")
                        .Add("interactions", persona.InteractionCount.ToString())
                        .Add("uptime_minutes", persona.Uptime.TotalMinutes.ToString("F1")),
                    CancellationToken.None);
                AnsiConsole.MarkupLine(OuroborosTheme.Ok($"  [OK] State saved (epoch {_networkStateProjector.CurrentEpoch}, {_networkStateProjector.RecentLearnings.Count} learnings)"));
                await _networkStateProjector.DisposeAsync();
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  [!] Failed to persist state: {ex.Message}"));
            }
        }

        // Dispose OpenClaw client
        if (OpenClawTools.SharedClient != null)
            await OpenClawTools.SharedClient.DisposeAsync();

        // Dispose room listener (if --room-mode was active)
        if (roomListener != null)
            await roomListener.DisposeAsync();

        // Dispose avatar subsystem
        if (_immersive != null)
            await _immersive.DisposeAsync();

        AnsiConsole.MarkupLine(Markup.Escape($"\n  Session complete. {persona.InteractionCount} interactions. Uptime: {persona.Uptime.TotalMinutes:F1} minutes."));

        // Dispose persona only if we own it (not provided by OuroborosAgent)
        if (ownedPersona != null)
            await ownedPersona.DisposeAsync();
    }
}
