// <copyright file="ImmersiveMode.RunAsync.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.CLI.Commands;

using System.Text;
using LangChain.DocumentLoaders;
using LangChain.Providers.Ollama;
using Ouroboros.Abstractions.Agent;
using Ouroboros.Application.Configuration;
using Ouroboros.Application.Extensions;
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

        // Declare variables needed by both try and finally (ShutdownAsync parameters)
        ImmersivePersona? ownedPersona = null;
        ImmersivePersona? persona = null;
        Ouroboros.CLI.Services.RoomPresence.AmbientRoomListener? roomListener = null;

        try
        {
            // Create the immersive persona (or use the one from OuroborosAgent master control)
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
                    ownedPersona = new ImmersivePersona(
                        personaName,
                        mettaEngine,
                        embeddingModel,
                        qdrantClient,
                        collectionRegistry);
                }
                else
                {
                    ownedPersona = new ImmersivePersona(
                        personaName,
                        mettaEngine,
                        embeddingModel,
                        options.QdrantEndpoint);
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

            // Initialize memory and cognitive subsystems
            await InitializeMemorySubsystemsAsync(options, embeddingModel, personaName, ct);
            await InitializeCognitiveSubsystemsAsync(options, embeddingModel, mettaEngine, ct);

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
            var conversationHistory = _tools.ConversationMemory.GetActiveHistory();
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
                    var toolNames = _tools.DynamicTools?.All.Select(t => t.Name).ToList() ?? [];
                    var skillNames = _tools.SkillRegistry?.GetAllSkills().Select(s => s.Name).ToList() ?? [];
                    persona.UpdateInnerDialogContext(input, toolNames, skillNames);

                    // Add user input to persistent memory
                    var lastUserMsg = conversationHistory.LastOrDefault(h => h.Role == "user").Content;
                    if (lastUserMsg != input)
                    {
                        conversationHistory.Add(("user", input));
                        if (_tools.ConversationMemory != null)
                        {
                            await _tools.ConversationMemory.AddTurnAsync("user", input, ct);
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
                    if (_tools.NetworkStateProjector != null)
                    {
                        await RecordInteractionLearningsAsync(input, response, persona, ct);
                    }

                    // Learn from distinction consciousness cycle (async, don't block)
                    LearnFromInteractionAsync(input, response, ct).ObserveExceptions("ImmersiveMode.LearnFromInteraction");

                    // Add assistant response to persistent memory
                    conversationHistory.Add(("assistant", response));
                    if (_tools.ConversationMemory != null)
                    {
                        await _tools.ConversationMemory.AddTurnAsync("assistant", response, ct);
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
                catch (InvalidOperationException ex)
                {
                    var errorFace = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
                    AnsiConsole.MarkupLine($"\n  [red]{Markup.Escape(errorFace)} {Markup.Escape($"✗ [error] {ex.Message}")}[/]");
                }
                catch (System.Net.Http.HttpRequestException ex)
                {
                    var errorFace = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
                    AnsiConsole.MarkupLine($"\n  [red]{Markup.Escape(errorFace)} {Markup.Escape($"✗ [error] {ex.Message}")}[/]");
                }
            }
        }
        finally
        {
            // Graceful shutdown: persist state, dispose resources
            // Always runs even if initialization or the main loop throws
            if (persona != null)
            {
                await ShutdownAsync(persona, ownedPersona, roomListener);
            }
        }
    }
}
