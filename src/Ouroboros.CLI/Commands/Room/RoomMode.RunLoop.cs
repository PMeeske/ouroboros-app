// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Commands;

using Microsoft.Extensions.DependencyInjection;
using LangChain.Providers.Ollama;
using Ouroboros.Abstractions.Monads;
using Ouroboros.Application.Configuration;
using Ouroboros.Application.Extensions;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Services;
using Ouroboros.CLI.Avatar;
using Ouroboros.ApiHost;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Services.RoomPresence;
using Ouroboros.CLI.Subsystems;
using Ouroboros.Core.CognitivePhysics;
using Ouroboros.Core.Ethics;
using Ouroboros.Providers;
using Ouroboros.Providers.TextToSpeech;
using Ouroboros.Speech;
using Ouroboros.Tools.MeTTa;
using Spectre.Console;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

/// <summary>
/// Partial class containing the main RunAsync loop implementation for RoomMode.
/// </summary>
public sealed partial class RoomMode
{
    // ── Main entry point ─────────────────────────────────────────────────────

    public async Task RunAsync(
        string personaName = "Iaret",
        string model       = "deepseek-v3.1:671b-cloud",
        string endpoint    = DefaultEndpoints.Ollama,
        string embedModel  = "nomic-embed-text",
        string qdrant      = DefaultEndpoints.QdrantGrpc,
        string? azureSpeechKey    = null,
        string azureSpeechRegion  = "eastus",
        string ttsVoice           = "en-US-JennyMultilingualNeural",
        bool   localTts           = false,
        bool   avatarOn           = true,
        int    avatarPort         = 9471,
        bool   quiet              = false,
        TimeSpan? cooldown        = null,
        int    maxPerWindow       = 8,
        double phiThreshold       = 0.05,
        bool   proactiveMode      = true,
        TimeSpan? idleSpeechDelay = null,
        bool   enableCamera       = false,
        CancellationToken ct      = default)
    {
        _lastInterjection = DateTime.MinValue;
        _recentInterjections.Clear();
        _lastUtteranceTime = DateTime.UtcNow;
        _lastProactiveSpeech = DateTime.MinValue;
        var interjectionCooldown = cooldown ?? TimeSpan.FromSeconds(20);
        var idleDelay = idleSpeechDelay ?? TimeSpan.FromSeconds(120);

        // ─── Banner ──────────────────────────────────────────────────────────
        AnsiConsole.MarkupLine($"\n  [rgb(148,103,189)]\u2554\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2557[/]");
        AnsiConsole.MarkupLine($"  [rgb(148,103,189)]\u2551   {Markup.Escape(personaName)} \u2014 Room Presence Mode                      \u2551[/]");
        AnsiConsole.MarkupLine($"  [rgb(148,103,189)]\u2551   Listening passively \u00b7 Ethics gated \u00b7 IIT \u03a6 aware   \u2551[/]");
        if (proactiveMode)
            AnsiConsole.MarkupLine($"  [rgb(148,103,189)]\u2551   Proactive mode ON \u00b7 Idle delay {idleDelay.TotalSeconds:F0}s              \u2551[/]");
        if (enableCamera)
            AnsiConsole.MarkupLine($"  [rgb(148,103,189)]\u2551   Camera presence + gesture detection enabled       \u2551[/]");
        AnsiConsole.MarkupLine($"  [rgb(148,103,189)]\u255a\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u255d[/]\n");

        // ─── 1. MeTTa engine ─────────────────────────────────────────────────
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("  [~] Initializing consciousness systems..."));
        using var mettaEngine = new InMemoryMeTTaEngine();

        // ─── 2. Embedding model (via SharedAgentBootstrap) ─────────────────────
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("  [~] Connecting to memory systems..."));
        var embeddingModel = Ouroboros.CLI.Services.SharedAgentBootstrap.CreateEmbeddingModel(
            endpoint, embedModel, msg => AnsiConsole.MarkupLine(msg.Contains("unavailable")
                ? OuroborosTheme.Warn($"  [!] {msg}")
                : OuroborosTheme.Ok($"  [OK] {msg}")));

        // ─── 3. ImmersivePersona (via SharedAgentBootstrap) ────────────────────
        await using var persona = await Services.SharedAgentBootstrap.CreateAndAwakenPersonaAsync(
            personaName, mettaEngine, embeddingModel, qdrant, ct,
            log: msg => AnsiConsole.MarkupLine(OuroborosTheme.Dim($"  [~] {msg}")));
        persona.AutonomousThought += (_, e) =>
        {
            if (e.Thought.Type is not (InnerThoughtType.Curiosity
                                    or InnerThoughtType.Observation
                                    or InnerThoughtType.SelfReflection))
                return;
            AnsiConsole.MarkupLine($"\n  [rgb(128,0,180)]\ud83d\udcad {Markup.Escape(e.Thought.Content)}[/]");
        };
        AnsiConsole.MarkupLine(OuroborosTheme.Ok($"  [OK] {personaName} is awake") + "\n");

        // ─── 4. ImmersiveSubsystem → avatar ───────────────────────────────────
        var immersive = new ImmersiveSubsystem();
        await immersive.InitializeStandaloneAsync(personaName, avatarOn, avatarPort, ct);

        // ─── 5. Ambient listener (via SharedAgentBootstrap) ────────────────────
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("  [~] Opening microphone..."));
        var stt = await Services.SharedAgentBootstrap.CreateSttService(
            azureSpeechKey, azureSpeechRegion,
            log: msg => AnsiConsole.MarkupLine(OuroborosTheme.Ok($"  [OK] {msg}")));
        if (stt == null)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn("  [!] No STT backend available \u2014 cannot listen."));
            return;
        }

        await using var listener = new AmbientRoomListener(stt);

        // ─── 6. PersonIdentifier ──────────────────────────────────────────────
        var ethicsFramework = EthicsFrameworkFactory.CreateDefault();
        var personIdentifier = new PersonIdentifier(persona.Personality, ethicsFramework, personaName);

        // ─── 7. AutonomousMind ────────────────────────────────────────────────
        var mind = new AutonomousMind();
        immersive.WirePersonaEvents(persona, mind);

        // Tune AutonomousMind for proactive room presence
        if (proactiveMode)
        {
            mind.Config.ShareDiscoveryProbability = 0.7;
            mind.Config.CuriosityIntervalSeconds = 60;
        }

        // ─── 8. LLM model for interjection decisions ──────────────────────────
        // RoomMode always creates its own model instance — it must NOT share the ImmersiveMode
        // model instance because concurrent calls on the same object corrupt the conversation state.
        // Both will call the same Ollama endpoint; the endpoint handles concurrency correctly.
        var settings = new ChatRuntimeSettings(0.8, 0, 60, false);
        IChatCompletionModel chatModel = new OllamaCloudChatModel(endpoint, "ollama", model, settings);

        // ─── 9. CognitivePhysics & Phi (via SharedAgentBootstrap) ──────────────
        var (cogPhysics, cogState) = Ouroboros.CLI.Services.SharedAgentBootstrap.CreateCognitivePhysics();
        var phiCalc = new IITPhiCalculator();
        _roomCogState = cogState;
        _roomLastTopic = "general";

        // ─── 9b. Episodic memory + causal reasoning (from DI or SharedAgentBootstrap fallback) ──
        _roomEpisodic = _serviceProvider?.GetService<Ouroboros.Pipeline.Memory.IEpisodicMemoryEngine>()
            ?? Ouroboros.CLI.Services.SharedAgentBootstrap.CreateEpisodicMemory(qdrant, embeddingModel);
        _roomCausalReasoning = _serviceProvider?.GetService<Ouroboros.Core.Reasoning.ICausalReasoningEngine>()
            ?? new Ouroboros.Core.Reasoning.CausalReasoningEngine();

        // ─── 9c. Neural-symbolic bridge (via SharedAgentBootstrap) ─────────────
        _roomNeuralSymbolic = Ouroboros.CLI.Services.SharedAgentBootstrap.CreateNeuralSymbolicBridge(
            chatModel, mettaEngine);

        // ─── 9d. Curiosity engine → AutonomousMind (via SharedAgentBootstrap) ──
        (_roomCuriosity, _roomSovereigntyGate) = Ouroboros.CLI.Services.SharedAgentBootstrap
            .CreateCuriosityAndSovereignty(chatModel, embeddingModel, mettaEngine, mind, ct);

        // ─── 10. TTS for interjections (via SharedAgentBootstrap) ──────────────
        var ttsService = Services.SharedAgentBootstrap.CreateTtsService(
            azureSpeechKey, azureSpeechRegion, personaName, ttsVoice,
            preferLocal: localTts,
            log: msg => AnsiConsole.MarkupLine(OuroborosTheme.Ok($"  [OK] {msg}")));

        // ─── 10b. Wire FFT voice detector — feed TTS audio for self-echo suppression ──
        if (ttsService is Ouroboros.Providers.TextToSpeech.AzureNeuralTtsService azureTtsForFft)
            azureTtsForFft.OnAudioSynthesized += wavData => listener.RegisterTtsAudio(wavData);

        // ─── 11. Rolling room transcript ─────────────────────────────────────
        var transcript = new List<(string SpeakerLabel, string Text, DateTime When)>();
        const int DisplayLines = 12;

        // ─── 12. Announce arrival (unless --quiet) ────────────────────────────
        if (!quiet)
        {
            var arrival = $"{personaName} is in the room.";
            AnsiConsole.MarkupLine(OuroborosTheme.Ok($"\n  {personaName}: {arrival}"));
            if (ttsService != null)
                await ttsService.SpeakAsync(arrival, null, ct).ConfigureAwait(false);
        }

        // ─── 13. Wire autonomous mind proactive messages ──────────────────────
        mind.OnProactiveMessage += async (msg) =>
        {
            if (string.IsNullOrWhiteSpace(msg)) return;
            var check = await ethicsFramework.EvaluateActionAsync(
                new ProposedAction
                {
                    ActionType   = "proactive_message",
                    Description  = $"Proactive autonomous thought: {msg[..Math.Min(80, msg.Length)]}",
                    Parameters   = new Dictionary<string, object> { ["length"] = msg.Length },
                    PotentialEffects = ["Speak unprompted to room participants"],
                },
                new ActionContext
                {
                    AgentId     = personaName,
                    Environment = "room_presence",
                    State       = new Dictionary<string, object> { ["mode"] = "ambient" },
                },
                CancellationToken.None).ConfigureAwait(false);

            if (!check.IsSuccess || !check.Value.IsPermitted) return;

            AnsiConsole.MarkupLine($"\n  [rgb(128,0,180)]\ud83d\udcad {Markup.Escape(personaName)}: {Markup.Escape(msg)}[/]");

            if (ttsService != null)
            {
                listener.NotifySelfSpeechStarted();
                try
                {
                    if (ttsService is AzureNeuralTtsService azureTtsP)
                    {
                        var msgLang = await LanguageSubsystem
                            .DetectStaticAsync(msg, CancellationToken.None).ConfigureAwait(false);
                        await azureTtsP.SpeakAsync(msg, msgLang.Culture, CancellationToken.None).ConfigureAwait(false);
                    }
                    else
                        await ttsService.SpeakAsync(msg, null, CancellationToken.None).ConfigureAwait(false);
                }
                finally { listener.NotifySelfSpeechEnded(); }
            }
        };

        mind.Start();

        // ─── 13b. Presence detection (camera-based) ─────────────────────────
        PresenceDetector? presenceDetector = null;
        GestureDetector? gestureDetector = null;

        if (enableCamera)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Dim("  [~] Enabling camera presence detection..."));

            // Use injected detector or create a standalone one
            presenceDetector = _presenceDetector ?? new PresenceDetector(new PresenceConfig
            {
                CheckIntervalSeconds = 5,
                PresenceThreshold = 0.5,
                UseWifi = false,
                UseCamera = true,
                UseInputActivity = false,
            });

            var lastAbsenceTime = DateTime.UtcNow;

            presenceDetector.OnPresenceDetected += (evt) =>
            {
                var awayDuration = DateTime.UtcNow - lastAbsenceTime;
                GreetOnPresenceAsync(
                    chatModel, ttsService, listener, immersive, personaName,
                    awayDuration.TotalMinutes > 1 ? awayDuration : null, ct)
                    .ObserveExceptions("RoomMode.GreetOnPresence");
            };

            presenceDetector.OnAbsenceDetected += (_) =>
            {
                lastAbsenceTime = DateTime.UtcNow;
                immersive.SetPresenceState("Idle", "contemplative");
                AnsiConsole.MarkupLine(OuroborosTheme.Dim("  [room] No one detected \u2014 switching to idle"));
            };

            presenceDetector.Start();
            AnsiConsole.MarkupLine(OuroborosTheme.Ok("  [OK] Camera presence detection active"));

            // ── Gesture detector ──────────────────────────────────────────
            gestureDetector = new GestureDetector();
            gestureDetector.OnGestureDetected += (gestureType, description) =>
            {
                RespondToGestureAsync(
                    gestureType, description,
                    ethicsFramework, chatModel, ttsService, listener, immersive,
                    personaName, ct)
                    .ObserveExceptions("RoomMode.RespondToGesture");
            };
            await gestureDetector.StartAsync(ct).ConfigureAwait(false);
            AnsiConsole.MarkupLine(OuroborosTheme.Ok("  [OK] Gesture detection active"));
        }

        // ─── 14. Main utterance handler ───────────────────────────────────────
        listener.OnUtterance += async (utterance) =>
        {
            // Track last utterance time for silence detection
            _lastUtteranceTime = DateTime.UtcNow;

            // Suppress utterances while Iaret is speaking — acoustic echo / coupling prevention.
            // The room mic picks up Iaret's TTS voice; we must not loop it back as input.
            if (_immersiveMode?.IsSpeaking ?? false)
                return;

            // ── Voice signature matching (biometric speaker ID) ────────────────
            // Try to match or update acoustic profile before text-based identification.
            string? voiceMatchedId = null;
            bool isOwnerVoice = false;
            if (utterance.Voice is { } sig)
            {
                // Check if this matches a known profile
                var match = _voiceSignatures.TryMatch(sig);
                if (match.HasValue)
                {
                    voiceMatchedId = match.Value.SpeakerId;
                    isOwnerVoice   = match.Value.IsOwner;
                    RoomIntentBus.FireSpeakerIdentified(
                        isOwnerVoice ? "User" : voiceMatchedId, isOwnerVoice);
                }
            }

            // ── Enrollment request ────────────────────────────────────────────
            if (utterance.Voice != null &&
                VoiceSignatureService.IsEnrollmentRequest(utterance.Text, personaName))
            {
                // Identify speaker via text style so we have a stable ID to pin
                var enrollPerson = await personIdentifier.IdentifyAsync(utterance, CancellationToken.None)
                                                         .ConfigureAwait(false);
                _voiceSignatures.EnrollOwner(enrollPerson.Id, utterance.Voice);
                var ack = $"I'll remember your voice. From now on I'll know it's you.";
                AnsiConsole.MarkupLine(OuroborosTheme.Ok($"\n  \u2726 {personaName}: {ack}"));
                RoomIntentBus.FireInterjection(personaName, ack);
                if (ttsService != null)
                {
                    listener.NotifySelfSpeechStarted();
                    try { await ttsService.SpeakAsync(ack, null, CancellationToken.None).ConfigureAwait(false); }
                    finally { listener.NotifySelfSpeechEnded(); }
                }
                return;
            }

            // ── Text-based speaker identification ─────────────────────────────
            var person  = await personIdentifier.IdentifyAsync(utterance, CancellationToken.None)
                                                .ConfigureAwait(false);

            // If voice matched the owner, override the text-style label with "User"
            var speaker = isOwnerVoice
                ? "User"
                : (person.Name ?? $"Person-{person.Id[..4]}");

            // Add the voice sample to the profile (builds up the fingerprint over time)
            if (utterance.Voice != null)
                _voiceSignatures.AddSample(person.Id, utterance.Voice);

            // ── Direct-address detection ──────────────────────────────────────
            // If the utterance mentions Iaret by name, treat it as a direct question —
            // publish to the intent bus so ImmersiveMode can show/handle it, and
            // bypass the SPEAK/SILENT LLM gate (always respond).
            bool isDirectAddress = utterance.Text.Contains(personaName, StringComparison.OrdinalIgnoreCase);
            if (isDirectAddress)
                RoomIntentBus.FireAddressedIaret(speaker, utterance.Text);

            // Update transcript display
            transcript.Add((speaker, utterance.Text, utterance.Timestamp));
            if (transcript.Count > 40) transcript.RemoveAt(0);
            PrintTranscript(transcript, DisplayLines, personaName);

            // Record to memory (ethics-gated inside)
            await personIdentifier.RecordUtteranceAsync(person, utterance.Text, CancellationToken.None)
                                  .ConfigureAwait(false);

            // Greet returning speaker if this is a known person (spoken greeting)
            if (!person.IsNewPerson() && person.InteractionCount > 1 && IsFirstUtteranceThisSession(person))
            {
                await GreetReturningPersonAsync(
                    person, speaker, personIdentifier,
                    chatModel, ttsService, listener, immersive,
                    personaName, ct).ConfigureAwait(false);
            }

            // Run interjection pipeline (pass isDirectAddress to force a response)
            await TryInterjectAsync(
                utterance, speaker, transcript,
                persona, personIdentifier, immersive,
                cogPhysics, phiCalc, phiThreshold,
                ethicsFramework, chatModel,
                ttsService, listener,
                interjectionCooldown, maxPerWindow,
                personaName, isDirectAddress, ct).ConfigureAwait(false);
        };

        await listener.StartAsync(ct).ConfigureAwait(false);
        AnsiConsole.MarkupLine(OuroborosTheme.Ok("  [OK] Room listener active \u2014 Ctrl+C to stop") + "\n");

        // ─── 15. Silence monitor (proactive speech when room is quiet) ───────
        var silenceMonitorTask = proactiveMode
            ? Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(15), ct).ConfigureAwait(false);
                        var silenceDuration = DateTime.UtcNow - _lastUtteranceTime;
                        var sinceLastProactive = DateTime.UtcNow - _lastProactiveSpeech;

                        if (silenceDuration >= idleDelay && sinceLastProactive >= idleDelay)
                        {
                            await TryProactiveSpeechAsync(
                                mind, immersive, ethicsFramework,
                                chatModel, ttsService, listener,
                                personaName, ct).ConfigureAwait(false);
                            _lastProactiveSpeech = DateTime.UtcNow;
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    catch (InvalidOperationException ex)
                    {
                        AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  [room] Proactive monitor error: {ex.Message}"));
                    }
                }
            }, ct)
            : Task.CompletedTask;

        // ─── 16. Keep running until cancelled ─────────────────────────────────
        try
        {
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }

        // ─── Cleanup ─────────────────────────────────────────────────────────
        await mind.StopAsync().ConfigureAwait(false);

        if (gestureDetector != null)
            await gestureDetector.DisposeAsync().ConfigureAwait(false);

        if (presenceDetector != null && presenceDetector != _presenceDetector)
        {
            await presenceDetector.StopAsync();
            presenceDetector.Dispose();
        }

        await immersive.DisposeAsync().ConfigureAwait(false);

        AnsiConsole.MarkupLine($"\n  [rgb(148,103,189)]{Markup.Escape(personaName)} has left the room. Goodbye.[/]");
    }
}
