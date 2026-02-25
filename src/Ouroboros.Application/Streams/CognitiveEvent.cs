// <copyright file="CognitiveEvent.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Streams;

using Ouroboros.Agent.MetaAI.Affect;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Services;
using Ouroboros.Domain.Autonomous;

/// <summary>
/// Identifies the cognitive stream domain.
/// Drives console display color, context section label, and throttle gap.
/// </summary>
public enum StreamKind
{
    Thought,
    Discovery,
    EmotionalChange,
    AutonomousAction,
    ActionEngine,
    InnerDialog,
    ConsciousnessShift,
    ValencePulse,
    PersonalityPulse,
    UserInteraction,
    CoordinatorMessage,
}

/// <summary>
/// Base type for all cognitive stream events.
/// <see cref="Summary"/> is a short human-readable description (≤80 chars)
/// computed at construction and used for console display and context injection.
/// </summary>
public abstract record CognitiveEvent(DateTime Timestamp, StreamKind Kind, string Summary);

// ── Thought ──────────────────────────────────────────────────────────────────

/// <summary>AutonomousMind.OnThought</summary>
public sealed record ThoughtEvent(Thought Thought, DateTime Timestamp)
    : CognitiveEvent(Timestamp, StreamKind.Thought,
        $"[{Thought.Type}] {(Thought.Content.Length > 72 ? Thought.Content[..72] + "…" : Thought.Content)}");

// ── Discovery ────────────────────────────────────────────────────────────────

/// <summary>AutonomousMind.OnDiscovery</summary>
public sealed record DiscoveryEvent(string Query, string Fact, DateTime Timestamp)
    : CognitiveEvent(Timestamp, StreamKind.Discovery,
        $"Discovered from '{(Query.Length > 30 ? Query[..30] + "…" : Query)}': {(Fact.Length > 40 ? Fact[..40] + "…" : Fact)}");

// ── Emotional change ─────────────────────────────────────────────────────────

/// <summary>AutonomousMind.OnEmotionalChange</summary>
public sealed record EmotionalChangeEvent(EmotionalState State, DateTime Timestamp)
    : CognitiveEvent(Timestamp, StreamKind.EmotionalChange,
        $"Emotion: {State.DominantEmotion} ({State.Description})");

// ── Autonomous action (from AutonomousMind loop) ──────────────────────────────

/// <summary>AutonomousMind.OnAction</summary>
public sealed record AutonomousActionEvent(AutonomousAction Action, DateTime Timestamp)
    : CognitiveEvent(Timestamp, StreamKind.AutonomousAction,
        $"Action: {(Action.Description.Length > 70 ? Action.Description[..70] + "…" : Action.Description)}");

// ── Action Engine (discrete 3-min step actions) ───────────────────────────────

/// <summary>AutonomousActionEngine.OnAction</summary>
public sealed record ActionEngineEvent(string Reason, string Result, DateTime Timestamp)
    : CognitiveEvent(Timestamp, StreamKind.ActionEngine,
        $"Step: {(Reason.Length > 72 ? Reason[..72] + "…" : Reason)}");

// ── Inner dialog (ImmersivePersona autonomous thoughts) ───────────────────────

/// <summary>ImmersivePersona.AutonomousThought</summary>
public sealed record InnerDialogEvent(InnerThought Thought, DateTime Timestamp)
    : CognitiveEvent(Timestamp, StreamKind.InnerDialog,
        $"[{Thought.Type}] {(Thought.Content.Length > 68 ? Thought.Content[..68] + "…" : Thought.Content)}");

// ── Consciousness shift ───────────────────────────────────────────────────────

/// <summary>ImmersivePersona.ConsciousnessShift</summary>
public sealed record ConsciousnessShiftEvent(string? NewEmotion, double ArousalChange, DateTime Timestamp)
    : CognitiveEvent(Timestamp, StreamKind.ConsciousnessShift,
        $"Consciousness: {NewEmotion ?? "shift"} (Δarousal={ArousalChange:+0.00;-0.00})");

// ── Valence pulse (interval-sampled, context-only) ────────────────────────────

/// <summary>Observable.Interval(30s) sampling IValenceMonitor.GetCurrentState()</summary>
public sealed record ValencePulseEvent(AffectiveState State, DateTime Timestamp)
    : CognitiveEvent(Timestamp, StreamKind.ValencePulse,
        $"Affect: v={State.Valence:+0.00;-0.00} s={State.Stress:F2} c={State.Curiosity:F2} a={State.Arousal:F2}");

// ── Personality pulse (interval-sampled, context-only) ────────────────────────

/// <summary>Observable.Interval(60s) sampling PersonalityProfile</summary>
public sealed record PersonalityPulseEvent(string PersonaName, string TopTraits, string Mood, DateTime Timestamp)
    : CognitiveEvent(Timestamp, StreamKind.PersonalityPulse,
        $"Personality [{PersonaName}]: {(TopTraits.Length > 50 ? TopTraits[..50] + "…" : TopTraits)} | mood: {Mood}");

// ── User interaction ──────────────────────────────────────────────────────────

/// <summary>Emitted by RunLoop when the user sends input.</summary>
public sealed record UserInteractionEvent(string Input, DateTime Timestamp)
    : CognitiveEvent(Timestamp, StreamKind.UserInteraction,
        $"User: {(Input.Length > 72 ? Input[..72] + "…" : Input)}");

// ── Coordinator proactive message ─────────────────────────────────────────────

/// <summary>AutonomousCoordinator.OnProactiveMessage</summary>
public sealed record CoordinatorMessageEvent(ProactiveMessageEventArgs Message, DateTime Timestamp)
    : CognitiveEvent(Timestamp, StreamKind.CoordinatorMessage,
        $"Coordinator: {(Message.Message.Length > 65 ? Message.Message[..65] + "…" : Message.Message)}");
