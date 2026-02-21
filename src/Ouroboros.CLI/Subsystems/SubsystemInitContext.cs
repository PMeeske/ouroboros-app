// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Infrastructure;

/// <summary>
/// Shared initialization context passed to each subsystem during agent startup.
/// Provides configuration, diagnostic output, and peer subsystem references.
/// The agent (mediator) constructs this once and passes it to each subsystem in dependency order.
/// </summary>
public sealed record SubsystemInitContext
{
    /// <summary>Agent configuration (flags, endpoints, model names, etc.).</summary>
    public required OuroborosConfig Config { get; init; }

    /// <summary>Diagnostic output for RecordInit / WriteDebug.</summary>
    public required IConsoleOutput Output { get; init; }

    /// <summary>Primary voice service (for persona info, TTS).</summary>
    public required VoiceModeService VoiceService { get; init; }

    /// <summary>Static configuration for Azure credentials and device sections.</summary>
    public Microsoft.Extensions.Configuration.IConfiguration? StaticConfiguration { get; init; }

    // ── Peer subsystem references ──
    // Populated by the agent before calling InitializeAsync.
    // Each subsystem may query peer subsystem state that was set by earlier-initialized peers.

    public required VoiceSubsystem Voice { get; init; }
    public required ModelSubsystem Models { get; init; }
    public required ToolSubsystem Tools { get; init; }
    public required MemorySubsystem Memory { get; init; }
    public required CognitiveSubsystem Cognitive { get; init; }
    public required AutonomySubsystem Autonomy { get; init; }
    public required EmbodimentSubsystem Embodiment { get; init; }

    // ── Agent-level callbacks for cross-cutting concerns ──

    /// <summary>Agent's RegisterCameraCaptureTool method (for Tapo devices).</summary>
    public Action? RegisterCameraCaptureAction { get; init; }

    // ── Crush-inspired agentic infrastructure ──

    /// <summary>
    /// Interactive tool-approval broker (Crush-style [a]/[s]/[d] dialog).
    /// When set, sensitive tools block until the user grants or denies execution.
    /// </summary>
    public ToolPermissionBroker? PermissionBroker { get; init; }

    /// <summary>
    /// Non-blocking event bus that publishes tool and agent lifecycle events
    /// to any subscribed UI component without coupling agent to console.
    /// </summary>
    public EventBroker<AgentEvent>? AgentEventBus { get; init; }
}
