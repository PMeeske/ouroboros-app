// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

/// <summary>
/// Base interface for all agent subsystems.
/// Each subsystem manages a cohesive group of capabilities and owns its own lifecycle.
/// </summary>
public interface IAgentSubsystem : IAsyncDisposable
{
    /// <summary>
    /// Human-readable name for diagnostics and logging.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether the subsystem has been successfully initialized.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Initializes this subsystem using the shared context.
    /// Called by the agent mediator in dependency order during startup.
    /// </summary>
    Task InitializeAsync(SubsystemInitContext ctx);
}
