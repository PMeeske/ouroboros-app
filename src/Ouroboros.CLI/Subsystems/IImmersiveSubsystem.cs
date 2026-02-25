// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using Ouroboros.Application.Avatar;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Services;

/// <summary>
/// Manages avatar rendering, persona event bridging, and topic-aware expression for
/// immersive AI persona experiences.
///
/// Ownership scope:
/// - <see cref="InteractiveAvatarService"/> lifecycle (create → start → dispose)
/// - Persona autonomous-thought → avatar status bridging (mirrors OuroborosAgent.WirePersonaEvents)
/// - Topic classification from raw user input → avatar stage positioning
/// - Presence state forwarding helpers
/// </summary>
public interface IImmersiveSubsystem : IAgentSubsystem
{
    /// <summary>Avatar orchestration service. Null when avatar is disabled.</summary>
    InteractiveAvatarService? AvatarService { get; }

    /// <summary>
    /// Wires <see cref="ImmersivePersona"/> and <see cref="AutonomousMind"/> events to the
    /// avatar service. Safe to call before the avatar service is started; events simply
    /// no-op when <see cref="AvatarService"/> is null.
    /// </summary>
    void WirePersonaEvents(ImmersivePersona persona, AutonomousMind? mind = null);

    /// <summary>
    /// Classifies <paramref name="rawInput"/> into an avatar topic category and forwards
    /// the hint to the avatar service for stage positioning + micro-expression flash.
    /// No-ops when no matching category is found or avatar is disabled.
    /// </summary>
    void PushTopicHint(string rawInput);

    /// <summary>
    /// Forwards a presence-state change to the avatar service (Listening, Processing, Speaking, Idle).
    /// </summary>
    void SetPresenceState(string presenceState, string mood, double energy = 0.5, double positivity = 0.5);

    /// <summary>
    /// Standalone initialization for use without a full <see cref="SubsystemInitContext"/>.
    /// Launches the avatar viewer when <paramref name="avatarEnabled"/> is true.
    /// </summary>
    Task InitializeStandaloneAsync(
        string personaName, bool avatarEnabled, int avatarPort,
        CancellationToken ct = default);
}
