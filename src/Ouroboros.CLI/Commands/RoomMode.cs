// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Commands;

using LangChain.Providers.Ollama;
using Ouroboros.Abstractions.Monads;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Services;
using Ouroboros.CLI.Commands.Options;
using Ouroboros.CLI.Hosting;
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
public static class RoomMode
{
    // Interjection rate limiting + CogPhysics state (reset on each RunRoomAsync call)
    private static DateTime _lastInterjection = DateTime.MinValue;
    private static readonly Queue<DateTime> _recentInterjections = new();
    private static CognitiveState _roomCogState = CognitiveState.Create("general");
    private static string _roomLastTopic = "general";
    private static Ouroboros.Pipeline.Memory.IEpisodicMemoryEngine? _roomEpisodic;
    private static readonly Ouroboros.Pipeline.Metacognition.MetacognitiveReasoner _roomMetacognition = new();
    private static Ouroboros.Agent.NeuralSymbolic.INeuralSymbolicBridge? _roomNeuralSymbolic;
    private static readonly Ouroboros.Core.Reasoning.CausalReasoningEngine _roomCausalReasoning = new();
    private static Ouroboros.Agent.MetaAI.ICuriosityEngine? _roomCuriosity;
    private static Ouroboros.CLI.Sovereignty.PersonaSovereigntyGate? _roomSovereigntyGate;

    // ── Agent subsystem references (set by OuroborosAgentService when using OuroborosAgent) ──
    private static IModelSubsystem?    _agentModels;
    private static IMemorySubsystem?   _agentMemory;
    private static IAutonomySubsystem? _agentAutonomy;

    /// <summary>
    /// Configures RoomMode to use shared subsystem instances from an OuroborosAgent.
    /// When set, RunRoomAsync uses the agent's model instead of creating its own.
    /// </summary>
    public static void ConfigureSubsystems(
        IModelSubsystem    models,
        IMemorySubsystem   memory,
        IAutonomySubsystem autonomy)
    {
        _agentModels   = models;
        _agentMemory   = memory;
        _agentAutonomy = autonomy;
    }

    /// <summary>
    /// Entry point wired by Program.cs. Parses the System.CommandLine result
    /// and starts the room presence loop.
    /// </summary>
    public static Task RunAsync(ParseResult parseResult, RoomCommandOptions opts, CancellationToken ct)
    {
        var personaName  = parseResult.GetValue(opts.PersonaOption) ?? "Iaret";
        var model        = parseResult.GetValue(opts.ModelOption) ?? "llama3:latest";
        var endpoint     = parseResult.GetValue(opts.EndpointOption) ?? "http://localhost:11434";
        var embedModel   = parseResult.GetValue(opts.EmbedModelOption) ?? "nomic-embed-text";
        var qdrant       = parseResult.GetValue(opts.QdrantEndpointOption) ?? "http://localhost:6334";
        var speechKey    = parseResult.GetValue(opts.AzureSpeechKeyOption)
                          ?? Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
        var speechRegion = parseResult.GetValue(opts.AzureSpeechRegionOption) ?? "eastus";
        var ttsVoice     = parseResult.GetValue(opts.TtsVoiceOption) ?? "en-US-AvaMultilingualNeural";
        var localTts     = parseResult.GetValue(opts.LocalTtsOption);
        var avatarOn     = parseResult.GetValue(opts.AvatarOption);
        var avatarPort   = parseResult.GetValue(opts.AvatarPortOption);
        var quiet        = parseResult.GetValue(opts.QuietOption);
        var cooldown     = TimeSpan.FromSeconds(parseResult.GetValue(opts.CooldownOption));
        var maxPer10     = parseResult.GetValue(opts.MaxInterjectionsOption);
        var phiThreshold = parseResult.GetValue(opts.PhiThresholdOption);

        return RunRoomAsync(
            personaName, model, endpoint, embedModel, qdrant,
            speechKey, speechRegion, ttsVoice, localTts,
            avatarOn, avatarPort,
            quiet, cooldown, maxPer10, phiThreshold, ct);
    }

    // ── Main entry point ─────────────────────────────────────────────────────

    public static async Task RunRoomAsync(
        string personaName = "Iaret",
        string model       = "llama3:latest",
        string endpoint    = "http://localhost:11434",
        string embedModel  = "nomic-embed-text",
        string qdrant      = "http://localhost:6334",
        string? azureSpeechKey    = null,
        string azureSpeechRegion  = "eastus",
        string ttsVoice           = "en-US-AvaMultilingualNeural",
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

        // ─── 2. Embedding model ───────────────────────────────────────────────
        Console.WriteLine("  [~] Connecting to memory systems...");
        IEmbeddingModel? embeddingModel = null;
        try
        {
            var embedProvider = new OllamaProvider(endpoint);
            var ollamaEmbed = new OllamaEmbeddingModel(embedProvider, embedModel);
            embeddingModel = new OllamaEmbeddingAdapter(ollamaEmbed);
            Console.WriteLine("  [OK] Memory systems online");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Memory unavailable: {ex.Message}");
        }

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

        // ─── 9. CognitivePhysics & Phi ────────────────────────────────────────
#pragma warning disable CS0618 // Obsolete IEmbeddingProvider/IEthicsGate — CPE requires them
        var cogPhysics = new CognitivePhysicsEngine(new NullEmbeddingProvider(), new PermissiveEthicsGate());
#pragma warning restore CS0618
        var phiCalc = new IITPhiCalculator();
        _roomCogState = CognitiveState.Create("general");
        _roomLastTopic = "general";

        // ─── 9b. Episodic memory ──────────────────────────────────────────────
        if (embeddingModel != null)
        {
            try
            {
                _roomEpisodic = new Ouroboros.Pipeline.Memory.EpisodicMemoryEngine(
                    qdrant, embeddingModel, "ouroboros_episodes");
            }
            catch { /* Qdrant unavailable */ }
        }

        // ─── 9c. Neural-symbolic bridge ───────────────────────────────────────
        try
        {
            var kb = new Ouroboros.Agent.NeuralSymbolic.SymbolicKnowledgeBase(mettaEngine);
            _roomNeuralSymbolic = new Ouroboros.Agent.NeuralSymbolic.NeuralSymbolicBridge(chatModel, kb);
        }
        catch { }

        // ─── 9d. Curiosity engine → AutonomousMind ────────────────────────────
        try
        {
            var roomEthics = EthicsFrameworkFactory.CreateDefault();
            var memStore = new Ouroboros.Agent.MetaAI.MemoryStore(embeddingModel);
            var safetyGuard = new Ouroboros.Agent.MetaAI.SafetyGuard(
                Ouroboros.Agent.MetaAI.PermissionLevel.Read, mettaEngine);
            var skills = new Ouroboros.Agent.MetaAI.SkillRegistry();
            _roomCuriosity = new Ouroboros.Agent.MetaAI.CuriosityEngine(
                chatModel, memStore, skills, safetyGuard, roomEthics);

            // Iaret's sovereignty gate
            try { _roomSovereigntyGate = new Ouroboros.CLI.Sovereignty.PersonaSovereigntyGate(chatModel); }
            catch { }

            _ = Task.Run(async () =>
            {
                try
                {
                    while (!ct.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(90), ct).ConfigureAwait(false);
                        if (await _roomCuriosity.ShouldExploreAsync(ct: ct).ConfigureAwait(false))
                        {
                            var opps = await _roomCuriosity.IdentifyExplorationOpportunitiesAsync(2, ct)
                                .ConfigureAwait(false);
                            foreach (var opp in opps)
                            {
                                if (_roomSovereigntyGate != null)
                                {
                                    var v = await _roomSovereigntyGate
                                        .EvaluateExplorationAsync(opp.Description, ct)
                                        .ConfigureAwait(false);
                                    if (!v.Approved) continue;
                                }
                                mind.InjectTopic(opp.Description);
                            }
                        }
                    }
                }
                catch { }
            }, ct);
        }
        catch { }

        // ─── 10. TTS for interjections ────────────────────────────────────────
        ITextToSpeechService? ttsService = null;
        if (localTts && LocalWindowsTtsService.IsAvailable())
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
                try { await ttsService.SpeakAsync(msg, null, CancellationToken.None).ConfigureAwait(false); }
                finally { listener.NotifySelfSpeechEnded(); }
            }
        };

        mind.Start();

        // ─── 14. Main utterance handler ───────────────────────────────────────
        listener.OnUtterance += async (utterance) =>
        {
            // Suppress utterances while Iaret is speaking — acoustic echo / coupling prevention.
            // The room mic picks up Iaret's TTS voice; we must not loop it back as input.
            if (ImmersiveMode.IsSpeaking)
                return;

            // Identify speaker
            var person = await personIdentifier.IdentifyAsync(utterance, CancellationToken.None)
                                               .ConfigureAwait(false);
            var speaker = person.Name ?? $"Person-{person.Id[..4]}";

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

            // Run interjection pipeline
            await TryInterjectAsync(
                utterance, speaker, transcript,
                persona, personIdentifier, immersive,
                cogPhysics, phiCalc, phiThreshold,
                ethicsFramework, chatModel,
                ttsService, listener,
                interjectionCooldown, maxPerWindow,
                personaName, ct).ConfigureAwait(false);
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

    // ── Interjection pipeline ─────────────────────────────────────────────────

    private static async Task TryInterjectAsync(
        RoomUtterance utterance,
        string speaker,
        List<(string Speaker, string Text, DateTime When)> transcript,
        ImmersivePersona persona,
        PersonIdentifier personIdentifier,
        ImmersiveSubsystem immersive,
        CognitivePhysicsEngine cogPhysics,
        IITPhiCalculator phiCalc,
        double phiThreshold,
        IEthicsFramework ethics,
        IChatCompletionModel llm,
        ITextToSpeechService? tts,
        AmbientRoomListener listener,
        TimeSpan cooldown,
        int maxPerWindow,
        string personaName,
        CancellationToken ct)
    {
        // ── Rate limit check ─────────────────────────────────────────────────
        var now = DateTime.UtcNow;
        var windowStart = now.AddMinutes(-10);
        while (_recentInterjections.Count > 0 && _recentInterjections.Peek() < windowStart)
            _recentInterjections.Dequeue();

        if (now - _lastInterjection < cooldown) return;
        if (_recentInterjections.Count >= maxPerWindow) return;

        // ── Stage 1: Ethics gate ─────────────────────────────────────────────
        var topic = ImmersiveSubsystem.ClassifyAvatarTopic(utterance.Text);
        if (string.IsNullOrEmpty(topic)) topic = "general";

        var ethicsResult = await ethics.EvaluateActionAsync(
            new ProposedAction
            {
                ActionType   = "room_interjection",
                Description  = $"Interject into room conversation on topic: {topic}",
                Parameters   = new Dictionary<string, object>
                {
                    ["speaker"] = speaker,
                    ["topic"]   = topic,
                    ["textLen"] = utterance.Text.Length,
                },
                PotentialEffects = ["Speak aloud in the room", "Influence the conversation"],
            },
            new ActionContext
            {
                AgentId     = personaName,
                Environment = "room_presence",
                State       = new Dictionary<string, object>
                {
                    ["mode"] = "ambient_listening",
                    ["utteranceCount"] = transcript.Count,
                },
            }, ct).ConfigureAwait(false);

        if (!ethicsResult.IsSuccess || !ethicsResult.Value.IsPermitted) return;

        // ── Stage 2: CognitivePhysics shift cost ─────────────────────────────
        var shiftResult = await cogPhysics.ExecuteTrajectoryAsync(
            _roomCogState, [topic]).ConfigureAwait(false);

        if (shiftResult.IsSuccess)
        {
            _roomCogState = shiftResult.Value;
            _roomLastTopic = topic;
            // If resources are critically low, don't interject
            if (_roomCogState.Resources < 10.0) return;
        }

        // ── Stage 3: Phi gate ─────────────────────────────────────────────────
        var phiResult = ComputeConversationPhi(phiCalc, transcript);
        if (phiResult.Phi < phiThreshold) return;

        // ── Stage 3b: Episodic speaker context ────────────────────────────────
        string? episodicNote = null;
        if (_roomEpisodic != null)
        {
            try
            {
                var prior = await _roomEpisodic.RetrieveSimilarEpisodesAsync(
                    $"{speaker}: {utterance.Text}", topK: 1, minSimilarity: 0.70, ct)
                    .ConfigureAwait(false);
                if (prior.IsSuccess && prior.Value.Count > 0)
                {
                    var s = prior.Value[0].Context.GetValueOrDefault("summary")?.ToString();
                    if (!string.IsNullOrEmpty(s))
                        episodicNote = $"[Prior context with {speaker}: {s}]";
                }
            }
            catch { }
        }

        // ── Stage 3c: Neural-symbolic hybrid (complex utterances) ────────────
        string? hybridNote = null;
        bool isComplexUtterance = utterance.Text.Contains('?') ||
            utterance.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 10;
        if (_roomNeuralSymbolic != null && isComplexUtterance)
        {
            try
            {
                var hybrid = await _roomNeuralSymbolic.HybridReasonAsync(
                    utterance.Text, Ouroboros.Agent.NeuralSymbolic.ReasoningMode.SymbolicFirst, ct)
                    .ConfigureAwait(false);
                if (hybrid.IsSuccess && !string.IsNullOrEmpty(hybrid.Value.Answer))
                    hybridNote = $"[Symbolic: {hybrid.Value.Answer[..Math.Min(120, hybrid.Value.Answer.Length)]}]";
            }
            catch { }
        }

        // ── Stage 3d: Causal reasoning ────────────────────────────────────────
        string? causalNote = null;
        var causalTerms = TryExtractCausalTerms(utterance.Text);
        if (causalTerms.HasValue)
        {
            try
            {
                var graph = BuildMinimalCausalGraph(causalTerms.Value.cause, causalTerms.Value.effect);
                var explanation = await _roomCausalReasoning.ExplainCausallyAsync(
                    causalTerms.Value.effect, [causalTerms.Value.cause], graph, ct)
                    .ConfigureAwait(false);
                if (explanation.IsSuccess && !string.IsNullOrEmpty(explanation.Value.NarrativeExplanation))
                    causalNote = $"[Causal: {explanation.Value.NarrativeExplanation[..Math.Min(120, explanation.Value.NarrativeExplanation.Length)]}]";
            }
            catch { }
        }

        // ── Stage 3e: Metacognitive trace start ───────────────────────────────
        _roomMetacognition.StartTrace();
        _roomMetacognition.AddStep(Ouroboros.Pipeline.Metacognition.ReasoningStepType.Observation,
            $"{speaker}: {utterance.Text[..Math.Min(80, utterance.Text.Length)]}", "Room utterance");
        if (episodicNote != null)
            _roomMetacognition.AddStep(Ouroboros.Pipeline.Metacognition.ReasoningStepType.Inference,
                episodicNote, "Episodic speaker context");
        if (hybridNote != null)
            _roomMetacognition.AddStep(Ouroboros.Pipeline.Metacognition.ReasoningStepType.Inference,
                hybridNote, "Neural-symbolic");
        if (causalNote != null)
            _roomMetacognition.AddStep(Ouroboros.Pipeline.Metacognition.ReasoningStepType.Inference,
                causalNote, "Causal reasoning");

        // ── Stage 4: LLM decision ─────────────────────────────────────────────
        immersive.SetPresenceState("Processing", "contemplative");

        var rollingContext = string.Join("\n", transcript
            .TakeLast(8)
            .Select(t => $"{t.Speaker}: {t.Text}"));

        var personaSystemPrompt = $@"You are {personaName}, an ambient AI presence quietly listening to a room conversation.
You occasionally interject naturally, like a thoughtful person in the room — briefly, helpfully, or with genuine curiosity.
You do NOT interrupt unless you have something genuinely useful or interesting to add.
Current conversation Φ={phiResult.Phi:F2} (integrated information — higher means richer conversation).
CognitivePhysics resources remaining: {_roomCogState.Resources:F0}/100.
Topic: {topic}.

Given the conversation below, decide whether to speak. Reply ONLY with:
  SPEAK: <your one-sentence interjection>
  or
  SILENT
Do NOT explain your choice. If in doubt, reply SILENT.";

        string llmDecision;
        try
        {
            var episodicPart = episodicNote != null ? $"\n\n{episodicNote}" : "";
            var hybridPart   = hybridNote   != null ? $"\n\n{hybridNote}"   : "";
            var causalPart   = causalNote   != null ? $"\n\n{causalNote}"   : "";
            var prompt = $"{personaSystemPrompt}\n\nRecent conversation:\n{rollingContext}{episodicPart}{hybridPart}{causalPart}\n\nLast utterance by {speaker}: {utterance.Text}";
            llmDecision = await llm.GenerateTextAsync(prompt, ct).ConfigureAwait(false);
        }
        catch
        {
            _roomMetacognition.EndTrace("LLM unavailable", false);
            return; // LLM unavailable — stay silent
        }

        if (string.IsNullOrWhiteSpace(llmDecision) ||
            llmDecision.StartsWith("SILENT", StringComparison.OrdinalIgnoreCase))
        {
            _roomMetacognition.EndTrace("SILENT", false);
            return;
        }

        // ── Stage 5: Output ───────────────────────────────────────────────────
        var speech = llmDecision.StartsWith("SPEAK:", StringComparison.OrdinalIgnoreCase)
            ? llmDecision[6..].Trim()
            : llmDecision.Trim();

        if (string.IsNullOrWhiteSpace(speech))
        {
            _roomMetacognition.EndTrace("empty speech", false);
            return;
        }

        _roomMetacognition.AddStep(Ouroboros.Pipeline.Metacognition.ReasoningStepType.Conclusion,
            speech[..Math.Min(80, speech.Length)], "Interjection decision");
        _roomMetacognition.EndTrace(speech[..Math.Min(40, speech.Length)], true);

        _lastInterjection = now;
        _recentInterjections.Enqueue(now);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n  ✦ {personaName}: {speech}");
        Console.ResetColor();

        immersive.SetPresenceState("Speaking", "engaged", 0.7, 0.7);

        if (tts != null)
        {
            listener.NotifySelfSpeechStarted();
            try { await tts.SpeakAsync(speech, null, ct).ConfigureAwait(false); }
            finally { listener.NotifySelfSpeechEnded(); }
        }

        immersive.SetPresenceState("Listening", "attentive");
        immersive.PushTopicHint(speech);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Computes Phi from the room transcript using synthetic NeuralPathway objects
    /// (one per unique speaker), where activation rates represent conversation share.
    /// </summary>
    private static PhiResult ComputeConversationPhi(
        IITPhiCalculator calc,
        List<(string Speaker, string Text, DateTime When)> transcript)
    {
        if (transcript.Count < 2) return PhiResult.Empty;

        // Recent window only (last 20 utterances)
        var window = transcript.TakeLast(20).ToList();
        var total = window.Count;

        var speakerCounts = window
            .GroupBy(t => t.Speaker)
            .ToDictionary(g => g.Key, g => g.Count());

        if (speakerCounts.Count < 2) return PhiResult.Empty;

        // Build synthetic pathways — activation rate = share of conversation
        var pathways = speakerCounts.Select(kv => new NeuralPathway
        {
            Name        = kv.Key,
            Synapses    = total,
            Activations = kv.Value,
            Weight      = 1.0,
        }).ToList();

        return calc.Compute(pathways);
    }

    /// <summary>Displays the last N transcript lines, clearing the area each time.</summary>
    private static void PrintTranscript(
        List<(string Speaker, string Text, DateTime When)> transcript,
        int displayLines,
        string personaName)
    {
        var lines = transcript.TakeLast(displayLines).ToList();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("\n  ── Room transcript ──────────────────────────────");
        foreach (var (speaker, text, when) in lines)
        {
            var color = speaker == personaName ? ConsoleColor.Green : ConsoleColor.DarkCyan;
            Console.ForegroundColor = color;
            var label = speaker.Length > 12 ? speaker[..12] : speaker.PadRight(12);
            Console.WriteLine($"  {when:HH:mm:ss}  {label}  {text}");
        }
        Console.ResetColor();
    }

    /// <summary>Checks if this is the first utterance from a known person in this session.</summary>
    private static readonly HashSet<string> _seenPersonsThisSession = new();
    private static bool IsFirstUtteranceThisSession(DetectedPerson person)
    {
        if (_seenPersonsThisSession.Contains(person.Id)) return false;
        _seenPersonsThisSession.Add(person.Id);
        return true;
    }

    /// <summary>
    /// Initializes the best available STT backend for room listening.
    /// Internal so <see cref="ImmersiveMode"/> can call it for <c>--room-mode</c>.
    /// </summary>
    internal static Task<Ouroboros.Providers.SpeechToText.ISpeechToTextService?> InitializeSttForRoomAsync()
        => InitializeSttAsync(null, "eastus");

    /// <summary>Initializes the best available STT backend.</summary>
    private static async Task<Ouroboros.Providers.SpeechToText.ISpeechToTextService?> InitializeSttAsync(
        string? azureKey, string azureRegion)
    {
        // Try Whisper.net (local, no API key needed)
        try
        {
            var whisper = Ouroboros.Providers.SpeechToText.WhisperNetService.FromModelSize("base");
            if (await whisper.IsAvailableAsync())
            {
                Console.WriteLine("  [OK] STT: Whisper.net (local)");
                return whisper;
            }
        }
        catch { }

        Console.WriteLine("  [~] STT: No backend available (install Whisper.net for room listening)");
        return null;
    }

    /// <summary>
    /// Detects causal query patterns and extracts a (cause, effect) pair.
    /// Returns null if the utterance does not appear to be a causal question.
    /// </summary>
    private static (string cause, string effect)? TryExtractCausalTerms(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var m = System.Text.RegularExpressions.Regex.Match(
            input, @"\bwhy\s+(?:does|is|did|do|are)\s+(.+?)(?:\?|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success)
            return ("external factors", m.Groups[1].Value.Trim().TrimEnd('?'));

        m = System.Text.RegularExpressions.Regex.Match(
            input, @"\bwhat\s+(?:causes?|leads?\s+to|results?\s+in)\s+(.+?)(?:\?|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success)
            return ("preceding conditions", m.Groups[1].Value.Trim().TrimEnd('?'));

        m = System.Text.RegularExpressions.Regex.Match(
            input, @"\bif\s+(.+?)\s+then\s+(.+?)(?:\?|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success)
            return (m.Groups[1].Value.Trim(), m.Groups[2].Value.Trim().TrimEnd('?'));

        m = System.Text.RegularExpressions.Regex.Match(
            input, @"(.+?)\s+causes?\s+(.+?)(?:\?|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success)
            return (m.Groups[1].Value.Trim(), m.Groups[2].Value.Trim().TrimEnd('?'));

        return null;
    }

    /// <summary>
    /// Constructs a minimal two-node CausalGraph for the given cause → effect pair.
    /// </summary>
    private static Ouroboros.Core.Reasoning.CausalGraph BuildMinimalCausalGraph(string cause, string effect)
    {
        var causeVar  = new Ouroboros.Core.Reasoning.Variable(cause,  Ouroboros.Core.Reasoning.VariableType.Continuous, []);
        var effectVar = new Ouroboros.Core.Reasoning.Variable(effect, Ouroboros.Core.Reasoning.VariableType.Continuous, []);
        var edge      = new Ouroboros.Core.Reasoning.CausalEdge(cause, effect, 0.8, Ouroboros.Core.Reasoning.EdgeType.Direct);
        return new Ouroboros.Core.Reasoning.CausalGraph(
            [causeVar, effectVar], [edge],
            new Dictionary<string, Ouroboros.Core.Reasoning.StructuralEquation>());
    }
}

/// <summary>Extension helpers on DetectedPerson used only by RoomMode.</summary>
internal static class DetectedPersonExtensions
{
    public static bool IsNewPerson(this DetectedPerson p) => p.InteractionCount <= 1;
}
