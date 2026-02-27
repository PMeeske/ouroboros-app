// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Commands;

using Ouroboros.Application.Personality;
using Ouroboros.Application.Services;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Services.RoomPresence;
using Ouroboros.CLI.Subsystems;
using Ouroboros.Core.CognitivePhysics;
using Ouroboros.Core.Ethics;
using Ouroboros.Providers.TextToSpeech;
using Ouroboros.Speech;
using Spectre.Console;
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
            catch (Exception) { /* episodic recall failed — non-critical */ }
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
            catch (Exception) { /* hybrid reasoning failed — non-critical */ }
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
            catch (Exception) { /* causal reasoning failed — non-critical */ }
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

        AnsiConsole.MarkupLine(OuroborosTheme.Ok($"\n  ✦ {personaName}: {speech}"));

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

}
