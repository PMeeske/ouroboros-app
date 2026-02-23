// Copyright (c) Ouroboros. All rights reserved.

using MediatR;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Mediator.Notifications;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// Central MediatR notification handler that feeds every agent-observable event
/// into Iaret's <see cref="IAgentEventSink"/> so the Main Agent can react
/// proactively on her own cadence.
///
/// Because MediatR dispatches notifications to *all* registered handlers,
/// additional subsystems (avatar, logging, analytics) can independently
/// subscribe to the same notification types without coupling to each other.
/// </summary>
public sealed class IaretEventReactionHandler :
    INotificationHandler<PresenceChangedNotification>,
    INotificationHandler<RoomUtteranceNotification>,
    INotificationHandler<SpeakerIdentifiedNotification>,
    INotificationHandler<DeviceEventNotification>,
    INotificationHandler<ConsciousnessShiftedNotification>,
    INotificationHandler<AutonomousThoughtNotification>,
    INotificationHandler<ToolStartedNotification>,
    INotificationHandler<ToolCompletedNotification>,
    INotificationHandler<GoalExecutedNotification>,
    INotificationHandler<LearningCompletedNotification>,
    INotificationHandler<ReasoningCompletedNotification>
{
    private readonly OuroborosAgent _agent;

    public IaretEventReactionHandler(OuroborosAgent agent) => _agent = agent;

    // Helper — non-blocking enqueue into the agent's event sink.
    private Task Forward(AgentEventNotification notification)
    {
        (_agent as IAgentEventSink)?.Enqueue(notification);
        return Task.CompletedTask;
    }

    public Task Handle(PresenceChangedNotification n, CancellationToken ct) => Forward(n);
    public Task Handle(RoomUtteranceNotification n, CancellationToken ct) => Forward(n);
    public Task Handle(SpeakerIdentifiedNotification n, CancellationToken ct) => Forward(n);
    public Task Handle(DeviceEventNotification n, CancellationToken ct) => Forward(n);
    public Task Handle(ConsciousnessShiftedNotification n, CancellationToken ct) => Forward(n);
    public Task Handle(AutonomousThoughtNotification n, CancellationToken ct) => Forward(n);
    public Task Handle(ToolStartedNotification n, CancellationToken ct) => Forward(n);
    public Task Handle(ToolCompletedNotification n, CancellationToken ct) => Forward(n);
    public Task Handle(GoalExecutedNotification n, CancellationToken ct) => Forward(n);
    public Task Handle(LearningCompletedNotification n, CancellationToken ct) => Forward(n);
    public Task Handle(ReasoningCompletedNotification n, CancellationToken ct) => Forward(n);
}
