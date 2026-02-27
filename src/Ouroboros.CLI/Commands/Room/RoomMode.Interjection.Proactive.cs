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

        AnsiConsole.MarkupLine(OuroborosTheme.Dim("\n  ── Room transcript ──────────────────────────────"));
        foreach (var (speaker, text, when) in lines)
        {
            var markup = speaker == personaName ? "green" : "rgb(148,103,189)";
            var label = speaker.Length > 12 ? speaker[..12] : speaker.PadRight(12);
            AnsiConsole.MarkupLine($"  [{markup}]{when:HH:mm:ss}  {Markup.Escape(label)}  {Markup.Escape(text)}[/]");
        }
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

            AnsiConsole.MarkupLine($"\n  [rgb(128,0,180)]\U0001f4ad {Markup.Escape(personaName)}: {Markup.Escape(speech)}[/]");

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
        catch (Exception) { /* LLM/TTS failure — stay silent */ }
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

            AnsiConsole.MarkupLine(OuroborosTheme.Ok($"\n  \U0001f44b {personaName}: {speech}"));

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
        catch (Exception) { /* LLM/TTS failure — stay silent */ }
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

            AnsiConsole.MarkupLine($"\n  [rgb(148,103,189)]\U0001f44b {Markup.Escape(personaName)}: {Markup.Escape(speech)}[/]");

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
        catch (Exception) { /* LLM/TTS failure — stay silent */ }
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

            AnsiConsole.MarkupLine(OuroborosTheme.Ok($"\n  \u2726 {personaName}: {speech}"));

            RoomIntentBus.FireInterjection(personaName, speech);

            if (tts != null)
            {
                listener.NotifySelfSpeechStarted();
                try { await tts.SpeakAsync(speech, null, ct).ConfigureAwait(false); }
                finally { listener.NotifySelfSpeechEnded(); }
            }
        }
        catch (Exception) { /* LLM/TTS failure — stay silent */ }
    }
}
