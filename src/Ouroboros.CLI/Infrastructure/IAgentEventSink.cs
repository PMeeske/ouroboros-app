// Copyright (c) Ouroboros. All rights reserved.

using Ouroboros.CLI.Mediator.Notifications;

namespace Ouroboros.CLI.Infrastructure;

/// <summary>
/// Accepts agent-observable events for proactive processing.
/// The Main Agent (Iaret) implements this so that MediatR notification handlers
/// can feed events into the agent's consciousness / reaction loop without
/// tight coupling to <see cref="Commands.OuroborosAgent"/>.
/// </summary>
public interface IAgentEventSink
{
    /// <summary>
    /// Enqueues an event for the agent to process.  Non-blocking; the agent
    /// drains the queue on its own cadence.
    /// </summary>
    void Enqueue(AgentEventNotification notification);

    /// <summary>
    /// Returns the approximate number of events waiting to be processed.
    /// </summary>
    int PendingCount { get; }
}
