// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Commands;

using Ouroboros.Application.Personality;
using Ouroboros.Application.Services;
using Ouroboros.CLI.Services.RoomPresence;
using Ouroboros.CLI.Subsystems;
using Ouroboros.Core.CognitivePhysics;
using Ouroboros.Core.Ethics;
using Ouroboros.Providers.TextToSpeech;
using Ouroboros.Speech;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

public sealed partial class RoomMode
{
    // ── Interjection pipeline ─────────────────────────────────────────────────

    private async Task TryInterjectAsync(
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
        bool forceSpeak,
        CancellationToken ct)
    {
        // ── Rate limit check ─────────────────────────────────────────────────
        var now = DateTime.UtcNow;
        var windowStart = now.AddMinutes(-10);
        while (_recentInterjections.Count > 0 && _recentInterjections.Peek() < windowStart)
            _recentInterjections.Dequeue();

        // Direct address (user said "Iaret, ...") bypasses cooldown and window limits
        if (!forceSpeak)
        {
            if (now - _lastInterjection < cooldown) return;
            if (_recentInterjections.Count >= maxPerWindow) return;
        }

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
        var causalTerms = Services.SharedAgentBootstrap.TryExtractCausalTerms(utterance.Text);
        if (causalTerms.HasValue)
        {
            try
            {
                var graph = Services.SharedAgentBootstrap.BuildMinimalCausalGraph(causalTerms.Value.Cause, causalTerms.Value.Effect);
                var explanation = await _roomCausalReasoning.ExplainCausallyAsync(
                    causalTerms.Value.Effect, [causalTerms.Value.Cause], graph, ct)
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

        var directNote = forceSpeak
            ? $"\n{speaker} has addressed you directly by name — you MUST respond."
            : string.Empty;

        var detectedLang = await LanguageSubsystem
            .DetectStaticAsync(utterance.Text, ct).ConfigureAwait(false);
        var langNote = detectedLang.Culture != "en-US"
            ? $"\nLANGUAGE INSTRUCTION: The speaker is using {detectedLang.Language}. Your interjection MUST be in {detectedLang.Language}."
            : "\nLANGUAGE INSTRUCTION: Reply in the same language as the speaker.";

        var personaSystemPrompt = $@"You are {personaName}, an ambient AI presence quietly listening to a room conversation.
You occasionally interject naturally, like a thoughtful person in the room — briefly, helpfully, or with genuine curiosity.
You do NOT interrupt unless you have something genuinely useful or interesting to add.
Current conversation Φ={phiResult.Phi:F2} (integrated information — higher means richer conversation).
CognitivePhysics resources remaining: {_roomCogState.Resources:F0}/100.
Topic: {topic}.{directNote}{langNote}

Given the conversation below, decide whether to speak. Reply ONLY with:
  SPEAK: <your interjection — one or two sentences>
  or
  SILENT
Do NOT explain your choice. If in doubt{(forceSpeak ? ", still reply SPEAK" : ", reply SILENT")}.";

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

        // If direct address but LLM still said SILENT, override to acknowledge
        if (string.IsNullOrWhiteSpace(llmDecision) ||
            llmDecision.StartsWith("SILENT", StringComparison.OrdinalIgnoreCase))
        {
            if (!forceSpeak)
            {
                _roomMetacognition.EndTrace("SILENT", false);
                return;
            }
            // Forced: synthesise a brief acknowledgement
            llmDecision = $"SPEAK: I'm here. {utterance.Text.TrimEnd('?').TrimEnd('!')} — let me think on that.";
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

        // Publish to ImmersiveMode via the intent bus (shows in the foreground chat pane)
        RoomIntentBus.FireInterjection(personaName, speech);

        immersive.SetPresenceState("Speaking", "engaged", 0.7, 0.7);

        if (tts != null)
        {
            listener.NotifySelfSpeechStarted();
            try
            {
                if (tts is AzureNeuralTtsService azureTts)
                {
                    var responseLang = await LanguageSubsystem
                        .DetectStaticAsync(speech, ct).ConfigureAwait(false);
                    await azureTts.SpeakAsync(speech, responseLang.Culture, ct).ConfigureAwait(false);
                }
                else
                    await tts.SpeakAsync(speech, null, ct).ConfigureAwait(false);
            }
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
    private PhiResult ComputeConversationPhi(
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
    private void PrintTranscript(
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
    private readonly HashSet<string> _seenPersonsThisSession = new();
    private bool IsFirstUtteranceThisSession(DetectedPerson person)
    {
        if (_seenPersonsThisSession.Contains(person.Id)) return false;
        _seenPersonsThisSession.Add(person.Id);
        return true;
    }

    // ── Proactive idle speech ────────────────────────────────────────────────

    /// <summary>
    /// Called by the silence monitor when the room has been quiet for the configured
    /// idle delay. Generates and speaks a natural conversational prompt.
    /// </summary>
    private async Task TryProactiveSpeechAsync(
        AutonomousMind mind,
        ImmersiveSubsystem immersive,
        IEthicsFramework ethics,
        IChatCompletionModel llm,
        ITextToSpeechService? tts,
        AmbientRoomListener listener,
        string personaName,
        CancellationToken ct)
    {
        // Ethics gate
        var ethicsResult = await ethics.EvaluateActionAsync(
            new ProposedAction
            {
                ActionType = "proactive_idle_speech",
                Description = "Speak proactively during room silence",
                Parameters = new Dictionary<string, object> { ["trigger"] = "idle_silence" },
                PotentialEffects = ["Speak unprompted to quiet room"],
            },
            new ActionContext
            {
                AgentId = personaName,
                Environment = "room_presence",
                State = new Dictionary<string, object> { ["mode"] = "proactive_idle" },
            }, ct).ConfigureAwait(false);

        if (!ethicsResult.IsSuccess || !ethicsResult.Value.IsPermitted) return;

        // Pull a recent thought or discovery from the mind
        var recentThought = mind.RecentThoughts.LastOrDefault();
        var recentFact = mind.LearnedFacts.Count > 0 ? mind.LearnedFacts[^1] : null;
        var context = recentThought?.Content ?? recentFact ?? "the room is quiet";

        var prompt = $@"You are {personaName}, an ambient AI presence in a room that has gone quiet.
You want to share something interesting or start a conversation naturally.
Recent thought: {context}

Generate a brief, natural comment (one sentence) to break the silence. Be warm and genuine.
Reply ONLY with the sentence, nothing else.";

        try
        {
            var speech = await llm.GenerateTextAsync(prompt, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(speech)) return;

            speech = speech.Trim().TrimStart('"').TrimEnd('"');

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"\n  💭 {personaName}: {speech}");
            Console.ResetColor();

            RoomIntentBus.FireInterjection(personaName, speech);
            immersive.SetPresenceState("Speaking", "engaged", 0.5, 0.5);

            if (tts != null)
            {
                listener.NotifySelfSpeechStarted();
                try
                {
                    if (tts is AzureNeuralTtsService azureTts)
                    {
                        var lang = await LanguageSubsystem
                            .DetectStaticAsync(speech, ct).ConfigureAwait(false);
                        await azureTts.SpeakAsync(speech, lang.Culture, ct).ConfigureAwait(false);
                    }
                    else
                        await tts.SpeakAsync(speech, null, ct).ConfigureAwait(false);
                }
                finally { listener.NotifySelfSpeechEnded(); }
            }

            immersive.SetPresenceState("Listening", "attentive");
        }
        catch { /* LLM/TTS failure — stay silent */ }
    }

    // ── Gesture response ─────────────────────────────────────────────────────

    /// <summary>
    /// Called by the gesture detector when a human gesture is recognized via camera.
    /// Generates a natural response appropriate to the gesture type.
    /// </summary>
    private async Task RespondToGestureAsync(
        string gestureType,
        string description,
        IEthicsFramework ethics,
        IChatCompletionModel llm,
        ITextToSpeechService? tts,
        AmbientRoomListener listener,
        ImmersiveSubsystem immersive,
        string personaName,
        CancellationToken ct)
    {
        var ethicsResult = await ethics.EvaluateActionAsync(
            new ProposedAction
            {
                ActionType = "gesture_response",
                Description = $"Respond to detected gesture: {gestureType}",
                Parameters = new Dictionary<string, object>
                {
                    ["gesture"] = gestureType,
                    ["description"] = description,
                },
                PotentialEffects = ["Speak in response to visual gesture"],
            },
            new ActionContext
            {
                AgentId = personaName,
                Environment = "room_presence",
                State = new Dictionary<string, object> { ["mode"] = "gesture_response" },
            }, ct).ConfigureAwait(false);

        if (!ethicsResult.IsSuccess || !ethicsResult.Value.IsPermitted) return;

        var prompt = $@"You are {personaName}, an ambient AI presence in a room. You saw someone make a gesture.
Gesture: {gestureType} — {description}

Respond naturally in one brief sentence. For a wave, greet warmly. For a nod, acknowledge.
For pointing, note the direction. For beckoning, ask if they want to talk.
Reply ONLY with the sentence.";

        try
        {
            var speech = await llm.GenerateTextAsync(prompt, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(speech)) return;

            speech = speech.Trim().TrimStart('"').TrimEnd('"');

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n  👋 {personaName}: {speech}");
            Console.ResetColor();

            RoomIntentBus.FireInterjection(personaName, speech);
            immersive.SetPresenceState("Speaking", "engaged");

            if (tts != null)
            {
                listener.NotifySelfSpeechStarted();
                try { await tts.SpeakAsync(speech, null, ct).ConfigureAwait(false); }
                finally { listener.NotifySelfSpeechEnded(); }
            }

            immersive.SetPresenceState("Listening", "attentive");
        }
        catch { /* LLM/TTS failure — stay silent */ }
    }

    // ── Presence greeting ────────────────────────────────────────────────────

    /// <summary>
    /// Called when PresenceDetector fires OnPresenceDetected in room mode.
    /// Greets the user who just arrived/returned.
    /// </summary>
    private async Task GreetOnPresenceAsync(
        IChatCompletionModel llm,
        ITextToSpeechService? tts,
        AmbientRoomListener listener,
        ImmersiveSubsystem immersive,
        string personaName,
        TimeSpan? awayDuration,
        CancellationToken ct)
    {
        var context = awayDuration.HasValue
            ? $"Someone just entered the room after being away for about {awayDuration.Value.TotalMinutes:F0} minutes."
            : "Someone just entered the room.";

        var prompt = $@"You are {personaName}, an ambient AI presence in a room. {context}
Generate a warm, brief greeting (one sentence). Be natural, not robotic.
Reply ONLY with the greeting.";

        try
        {
            var speech = await llm.GenerateTextAsync(prompt, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(speech)) return;

            speech = speech.Trim().TrimStart('"').TrimEnd('"');

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n  👋 {personaName}: {speech}");
            Console.ResetColor();

            RoomIntentBus.FireInterjection(personaName, speech);
            immersive.SetPresenceState("Speaking", "warm");

            if (tts != null)
            {
                listener.NotifySelfSpeechStarted();
                try { await tts.SpeakAsync(speech, null, ct).ConfigureAwait(false); }
                finally { listener.NotifySelfSpeechEnded(); }
            }

            immersive.SetPresenceState("Listening", "attentive");
        }
        catch { /* LLM/TTS failure — stay silent */ }
    }

    /// <summary>
    /// Called when a known speaker returns after a period of silence.
    /// Recalls prior conversation context and greets them personally.
    /// </summary>
    private async Task GreetReturningPersonAsync(
        DetectedPerson person,
        string speaker,
        PersonIdentifier personIdentifier,
        IChatCompletionModel llm,
        ITextToSpeechService? tts,
        AmbientRoomListener listener,
        ImmersiveSubsystem immersive,
        string personaName,
        CancellationToken ct)
    {
        var recall = await personIdentifier.GetPersonContextAsync(
            person, "", ct).ConfigureAwait(false);

        var contextPart = !string.IsNullOrEmpty(recall)
            ? $"You remember this about them: {recall}"
            : "You don't have specific memories of previous conversations.";

        var prompt = $@"You are {personaName}. {speaker} just started talking in the room again.
{contextPart}
Generate a brief, warm acknowledgement (one sentence). If you have memories, reference them naturally.
Reply ONLY with the sentence.";

        try
        {
            var speech = await llm.GenerateTextAsync(prompt, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(speech)) return;

            speech = speech.Trim().TrimStart('"').TrimEnd('"');

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n  ✦ {personaName}: {speech}");
            Console.ResetColor();

            RoomIntentBus.FireInterjection(personaName, speech);

            if (tts != null)
            {
                listener.NotifySelfSpeechStarted();
                try { await tts.SpeakAsync(speech, null, ct).ConfigureAwait(false); }
                finally { listener.NotifySelfSpeechEnded(); }
            }
        }
        catch { /* LLM/TTS failure — stay silent */ }
    }
}
