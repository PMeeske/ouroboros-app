using MediatR;
using Ouroboros.CLI.Mediator;
using Ouroboros.CLI.Mediator.Notifications;

namespace Ouroboros.Tests.CLI.Mediator.Handlers;

/// <summary>
/// Verifies the IaretEventReactionHandler implements all expected notification interfaces.
/// </summary>
[Trait("Category", "Unit")]
public class IaretEventReactionHandlerTests
{
    [Fact]
    public void IsSealed()
    {
        typeof(IaretEventReactionHandler).IsSealed.Should().BeTrue();
    }

    [Theory]
    [InlineData(typeof(INotificationHandler<PresenceChangedNotification>))]
    [InlineData(typeof(INotificationHandler<RoomUtteranceNotification>))]
    [InlineData(typeof(INotificationHandler<SpeakerIdentifiedNotification>))]
    [InlineData(typeof(INotificationHandler<DeviceEventNotification>))]
    [InlineData(typeof(INotificationHandler<ConsciousnessShiftedNotification>))]
    [InlineData(typeof(INotificationHandler<AutonomousThoughtNotification>))]
    [InlineData(typeof(INotificationHandler<ToolStartedNotification>))]
    [InlineData(typeof(INotificationHandler<ToolCompletedNotification>))]
    [InlineData(typeof(INotificationHandler<GoalExecutedNotification>))]
    [InlineData(typeof(INotificationHandler<LearningCompletedNotification>))]
    [InlineData(typeof(INotificationHandler<ReasoningCompletedNotification>))]
    public void ImplementsNotificationHandler(Type interfaceType)
    {
        typeof(IaretEventReactionHandler).Should().Implement(interfaceType);
    }

    [Fact]
    public void HasOuroborosAgentConstructor()
    {
        var ctor = typeof(IaretEventReactionHandler).GetConstructors().First();
        var paramTypes = ctor.GetParameters().Select(p => p.ParameterType.Name).ToArray();
        paramTypes.Should().Contain("OuroborosAgent");
    }

    [Fact]
    public void ImplementsExactly11NotificationHandlers()
    {
        var notificationHandlerInterfaces = typeof(IaretEventReactionHandler)
            .GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INotificationHandler<>))
            .ToList();

        notificationHandlerInterfaces.Should().HaveCount(11);
    }
}
