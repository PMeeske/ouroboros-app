// Copyright (c) Ouroboros. All rights reserved.

using MediatR;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Services;

namespace Ouroboros.CLI.Mediator.Notifications;

/// <summary>
/// Base type for all agent-observable events published through the MediatR
/// notification pipeline.  Iaret (the Main Agent) — and any other subsystem —
/// can implement <see cref="INotificationHandler{T}"/> for these to react
/// proactively when something happens in the environment.
/// </summary>
public abstract record AgentEventNotification(
    DateTime Timestamp,
    string Source) : INotification;

// ═══════════════════════════════════════════════════════════════════════════════
// Presence
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Presence state changed (user arrived / departed).
/// Wraps the existing <see cref="PresenceEvent"/> from the PresenceDetector.
/// </summary>
public sealed record PresenceChangedNotification(
    PresenceEvent Event)
    : AgentEventNotification(Event.Timestamp, $"presence:{Event.Source}");

// ═══════════════════════════════════════════════════════════════════════════════
// Room / Voice
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Somebody spoke in the room.  <see cref="IsAddressingAgent"/> is true when
/// the speaker explicitly addressed Iaret by name.
/// </summary>
public sealed record RoomUtteranceNotification(
    string Speaker,
    string Text,
    bool IsAddressingAgent)
    : AgentEventNotification(DateTime.UtcNow, "room");

/// <summary>
/// A speaker was identified (or re-identified) by voice signature.
/// </summary>
public sealed record SpeakerIdentifiedNotification(
    string SpeakerLabel,
    bool IsOwner)
    : AgentEventNotification(DateTime.UtcNow, "voice");

// ═══════════════════════════════════════════════════════════════════════════════
// Devices (Tapo cameras, IoT sensors, ...)
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// A physical device raised an event — motion detected on a Tapo camera,
/// sensor reading changed, etc.
/// </summary>
public sealed record DeviceEventNotification(
    string DeviceType,
    string DeviceId,
    string EventKind,
    string? Payload = null)
    : AgentEventNotification(DateTime.UtcNow, $"device:{DeviceType}");

// ═══════════════════════════════════════════════════════════════════════════════
// Persona / Consciousness
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Iaret's consciousness shifted — significant emotion or arousal change.
/// </summary>
public sealed record ConsciousnessShiftedNotification(
    string? NewEmotion,
    double ArousalChange,
    ConsciousnessState State)
    : AgentEventNotification(DateTime.UtcNow, "persona");

/// <summary>
/// An autonomous thought surfaced from the persona's inner dialog.
/// </summary>
public sealed record AutonomousThoughtNotification(
    InnerThought Thought)
    : AgentEventNotification(DateTime.UtcNow, "persona:thought");

// ═══════════════════════════════════════════════════════════════════════════════
// Tools
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// A tool execution started.
/// </summary>
public sealed record ToolStartedNotification(
    string ToolName,
    string? Parameter)
    : AgentEventNotification(DateTime.UtcNow, "tools");

/// <summary>
/// A tool execution completed (successfully or with error).
/// </summary>
public sealed record ToolCompletedNotification(
    string ToolName,
    bool Success,
    string? Output,
    TimeSpan Elapsed)
    : AgentEventNotification(DateTime.UtcNow, "tools");

// ═══════════════════════════════════════════════════════════════════════════════
// Autonomy / Goals
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// An autonomous goal was executed.
/// </summary>
public sealed record GoalExecutedNotification(
    string Goal,
    bool Success,
    TimeSpan Duration)
    : AgentEventNotification(DateTime.UtcNow, "autonomy");

/// <summary>
/// A learning episode completed (skills acquired, rules learned).
/// </summary>
public sealed record LearningCompletedNotification(
    int EpisodesProcessed,
    int RulesLearned)
    : AgentEventNotification(DateTime.UtcNow, "learning");

/// <summary>
/// A reasoning chain completed with a result.
/// </summary>
public sealed record ReasoningCompletedNotification(
    string Query,
    string Answer,
    double Confidence)
    : AgentEventNotification(DateTime.UtcNow, "reasoning");
