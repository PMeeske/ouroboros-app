// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Commands;

using LangChain.Providers.Ollama;
using Ouroboros.Abstractions.Monads;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Services;
using Ouroboros.CLI.Commands.Options;
using Ouroboros.ApiHost;
using Ouroboros.CLI.Services.RoomPresence;
using Ouroboros.CLI.Subsystems;
using Ouroboros.Core.CognitivePhysics;
using Ouroboros.Core.Ethics;
using Ouroboros.Providers;
using Ouroboros.Providers.TextToSpeech;
using Ouroboros.Speech;
using Ouroboros.Tools.MeTTa;
using System.CommandLine;
using System.CommandLine.Parsing;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

/// <summary>
/// Iaret as ambient AI room presence.
///
/// Continuously listens to the room microphone, identifies speakers by
/// communication style (persisted to Qdrant via PersonalityEngine), and
/// proactively interjects using a five-stage pipeline:
///
///   Ethics gate  → CognitivePhysics shift cost  → Phi (IIT)  → LLM decision  → TTS + console
///
/// Launch with: <c>ouroboros room</c> or <c>ouroboros room --quiet</c>
/// </summary>
public sealed partial class RoomMode
{
    // Interjection rate limiting + CogPhysics state (reset on each RunAsync call)
    private DateTime _lastInterjection = DateTime.MinValue;
    private readonly Queue<DateTime> _recentInterjections = new();
    private CognitiveState _roomCogState = CognitiveState.Create("general");
    private string _roomLastTopic = "general";
    private Ouroboros.Pipeline.Memory.IEpisodicMemoryEngine? _roomEpisodic;
    private readonly Ouroboros.Pipeline.Metacognition.MetacognitiveReasoner _roomMetacognition = new();
    private Ouroboros.Agent.NeuralSymbolic.INeuralSymbolicBridge? _roomNeuralSymbolic;
    private readonly Ouroboros.Core.Reasoning.CausalReasoningEngine _roomCausalReasoning = new();
    private Ouroboros.Agent.MetaAI.ICuriosityEngine? _roomCuriosity;
    private Ouroboros.CLI.Sovereignty.PersonaSovereigntyGate? _roomSovereigntyGate;

    // ── Agent subsystem references ────────────────────────────────────────────
    private readonly IModelSubsystem?    _agentModels;
    private readonly IMemorySubsystem?   _agentMemory;
    private readonly IAutonomySubsystem? _agentAutonomy;

    // ── ImmersiveMode reference for IsSpeaking check ──────────────────────────
    private readonly ImmersiveMode? _immersiveMode;

    // ── Voice signature service (speaker biometric identification) ─────────────
    private readonly VoiceSignatureService _voiceSignatures = new();

    /// <summary>
    /// Creates a RoomMode instance wired to the agent's subsystems.
    /// </summary>
    public RoomMode(
        ImmersiveMode?    immersiveMode = null,
        IModelSubsystem?    agentModels   = null,
        IMemorySubsystem?   agentMemory   = null,
        IAutonomySubsystem? agentAutonomy = null)
    {
        _immersiveMode = immersiveMode;
        _agentModels   = agentModels;
        _agentMemory   = agentMemory;
        _agentAutonomy = agentAutonomy;
    }

    /// <summary>
    /// Entry point wired by Program.cs. Parses the System.CommandLine result
    /// and starts the room presence loop.
    /// </summary>
    public Task RunAsync(ParseResult parseResult, RoomCommandOptions opts, CancellationToken ct)
    {
        var personaName  = parseResult.GetValue(opts.PersonaOption) ?? "Iaret";
        var model        = parseResult.GetValue(opts.ModelOption) ?? "llama3:latest";
        var endpoint     = parseResult.GetValue(opts.EndpointOption) ?? "http://localhost:11434";
        var embedModel   = parseResult.GetValue(opts.EmbedModelOption) ?? "nomic-embed-text";
        var qdrant       = parseResult.GetValue(opts.QdrantEndpointOption) ?? "http://localhost:6334";
        var speechKey    = parseResult.GetValue(opts.AzureSpeechKeyOption)
                          ?? Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
        var speechRegion = parseResult.GetValue(opts.AzureSpeechRegionOption) ?? "eastus";
        var ttsVoice     = parseResult.GetValue(opts.TtsVoiceOption) ?? "en-US-JennyMultilingualNeural";
        var localTts     = parseResult.GetValue(opts.LocalTtsOption);
        var avatarOn     = parseResult.GetValue(opts.AvatarOption);
        var avatarPort   = parseResult.GetValue(opts.AvatarPortOption);
        var quiet        = parseResult.GetValue(opts.QuietOption);
        var cooldown     = TimeSpan.FromSeconds(parseResult.GetValue(opts.CooldownOption));
        var maxPer10     = parseResult.GetValue(opts.MaxInterjectionsOption);
        var phiThreshold = parseResult.GetValue(opts.PhiThresholdOption);

        return RunAsync(
            personaName, model, endpoint, embedModel, qdrant,
            speechKey, speechRegion, ttsVoice, localTts,
            avatarOn, avatarPort,
            quiet, cooldown, maxPer10, phiThreshold, ct);
    }

    // ── Main entry point ─────────────────────────────────────────────────────

    public async Task RunAsync(
        string personaName = "Iaret",
        string model       = "llama3:latest",
        string endpoint    = "http://localhost:11434",
        string embedModel  = "nomic-embed-text",
        string qdrant      = "http://localhost:6334",
        string? azureSpeechKey    = null,
        string azureSpeechRegion  = "eastus",
        string ttsVoice           = "en-US-JennyMultilingualNeural",
        bool   localTts           = false,
        bool   avatarOn           = true,
        int    avatarPort         = 9471,
        bool   quiet              = false,
        TimeSpan? cooldown        = null,
        int    maxPerWindow       = 4,
        double phiThreshold       = 0.15,
        CancellationToken ct      = default)
    {
        _lastInterjection = DateTime.MinValue;
        _recentInterjections.Clear();
        var interjectionCooldown = cooldown ?? TimeSpan.FromSeconds(45);

        // ─── Banner ──────────────────────────────────────────────────────────
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n  ╔══════════════════════════════════════════════════════╗");
        Console.WriteLine($"  ║   {personaName} — Room Presence Mode                      ║");
        Console.WriteLine($"  ║   Listening passively · Ethics gated · IIT Φ aware   ║");
        Console.WriteLine($"  ╚══════════════════════════════════════════════════════╝\n");
        Console.ResetColor();

        // ─── 1. MeTTa engine ─────────────────────────────────────────────────
        Console.WriteLine("  [~] Initializing consciousness systems...");
        using var mettaEngine = new InMemoryMeTTaEngine();

        // ─── 2. Embedding model (via SharedAgentBootstrap) ─────────────────────
        Console.WriteLine("  [~] Connecting to memory systems...");
        var embeddingModel = Ouroboros.CLI.Services.SharedAgentBootstrap.CreateEmbeddingModel(
            endpoint, embedModel, msg => Console.WriteLine($"  [{(msg.Contains("unavailable") ? "!" : "OK")}] {msg}"));

        // ─── 3. ImmersivePersona ──────────────────────────────────────────────
        Console.WriteLine("  [~] Awakening persona...");
        await using var persona = new ImmersivePersona(personaName, mettaEngine, embeddingModel, qdrant);
        persona.AutonomousThought += (_, e) =>
        {
            if (e.Thought.Type is not (InnerThoughtType.Curiosity
                                    or InnerThoughtType.Observation
                                    or InnerThoughtType.SelfReflection))
                return;
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine($"\n  💭 {e.Thought.Content}");
            Console.ResetColor();
        };
        await persona.AwakenAsync(ct);
        Console.WriteLine($"  [OK] {personaName} is awake\n");

        // ─── 4. ImmersiveSubsystem → avatar ───────────────────────────────────
        var immersive = new ImmersiveSubsystem();
        await immersive.InitializeStandaloneAsync(personaName, avatarOn, avatarPort, ct);

        // ─── 5. Ambient listener ──────────────────────────────────────────────
        Console.WriteLine("  [~] Opening microphone...");
        var stt = await InitializeSttAsync(azureSpeechKey, azureSpeechRegion);
        if (stt == null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  [!] No STT backend available — cannot listen.");
            Console.ResetColor();
            return;
        }

        await using var listener = new AmbientRoomListener(stt);

        // ─── 6. PersonIdentifier ──────────────────────────────────────────────
        var ethicsFramework = EthicsFrameworkFactory.CreateDefault();
        var personIdentifier = new PersonIdentifier(persona.Personality, ethicsFramework, personaName);

        // ─── 7. AutonomousMind ────────────────────────────────────────────────
        var mind = new AutonomousMind();
        immersive.WirePersonaEvents(persona, mind);

        // ─── 8. LLM model for interjection decisions ──────────────────────────
        // RoomMode always creates its own model instance — it must NOT share the ImmersiveMode
        // model instance because concurrent calls on the same object corrupt the conversation state.
        // Both will call the same Ollama endpoint; the endpoint handles concurrency correctly.
        var settings = new ChatRuntimeSettings(0.8, 256, 60, false);
        IChatCompletionModel chatModel = new OllamaCloudChatModel(endpoint, "ollama", model, settings);

        // ─── 9. CognitivePhysics & Phi (via SharedAgentBootstrap) ──────────────
        var (cogPhysics, cogState) = Ouroboros.CLI.Services.SharedAgentBootstrap.CreateCognitivePhysics();
        var phiCalc = new IITPhiCalculator();
        _roomCogState = cogState;
        _roomLastTopic = "general";

        // ─── 9b. Episodic memory (via SharedAgentBootstrap) ────────────────────
        _roomEpisodic = Ouroboros.CLI.Services.SharedAgentBootstrap.CreateEpisodicMemory(
            qdrant, embeddingModel);

        // ─── 9c. Neural-symbolic bridge (via SharedAgentBootstrap) ─────────────
        _roomNeuralSymbolic = Ouroboros.CLI.Services.SharedAgentBootstrap.CreateNeuralSymbolicBridge(
            chatModel, mettaEngine);

        // ─── 9d. Curiosity engine → AutonomousMind (via SharedAgentBootstrap) ──
        (_roomCuriosity, _roomSovereigntyGate) = Ouroboros.CLI.Services.SharedAgentBootstrap
            .CreateCuriosityAndSovereignty(chatModel, embeddingModel, mettaEngine, mind, ct);

        // ─── 10. TTS for interjections ────────────────────────────────────────
        ITextToSpeechService? ttsService = null;
        if (!string.IsNullOrEmpty(azureSpeechKey))
        {
            try
            {
                ttsService = new AzureNeuralTtsService(azureSpeechKey, azureSpeechRegion, personaName);
                Console.WriteLine($"  [OK] Voice output: Azure Neural TTS ({ttsVoice})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [!] Azure TTS unavailable: {ex.Message}");
            }
        }
        if (ttsService == null && localTts && LocalWindowsTtsService.IsAvailable())
        {
            try { ttsService = new LocalWindowsTtsService(rate: 1, volume: 100, useEnhancedProsody: true); }
            catch { }
        }

        // ─── 11. Rolling room transcript ─────────────────────────────────────
        var transcript = new List<(string SpeakerLabel, string Text, DateTime When)>();
        const int DisplayLines = 12;

        // ─── 12. Announce arrival (unless --quiet) ────────────────────────────
        if (!quiet)
        {
            var arrival = $"{personaName} is in the room.";
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n  {personaName}: {arrival}");
            Console.ResetColor();
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

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"\n  💭 {personaName}: {msg}");
            Console.ResetColor();

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

        // ─── 14. Main utterance handler ───────────────────────────────────────
        listener.OnUtterance += async (utterance) =>
        {
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
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n  ✦ {personaName}: {ack}");
                Console.ResetColor();
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

            // Greet returning speaker if this is a known person
            if (!person.IsNewPerson() && person.InteractionCount > 1 && IsFirstUtteranceThisSession(person))
            {
                var recall = await personIdentifier.GetPersonContextAsync(
                    person, utterance.Text, CancellationToken.None).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(recall))
                    immersive.SetPresenceState("Speaking", "warm");
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
        Console.WriteLine("  [OK] Room listener active — Ctrl+C to stop\n");

        // ─── 15. Keep running until cancelled ─────────────────────────────────
        try
        {
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }

        // ─── Cleanup ─────────────────────────────────────────────────────────
        await mind.StopAsync().ConfigureAwait(false);
        await immersive.DisposeAsync().ConfigureAwait(false);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n  {personaName} has left the room. Goodbye.");
        Console.ResetColor();
    }
}
