using MediatR;
using Ouroboros.CLI.Mediator.Notifications;

namespace Ouroboros.Tests.CLI.Mediator.Notifications;

[Trait("Category", "Unit")]
public class AgentEventNotificationTests
{
    [Fact]
    public void RoomUtteranceNotification_SetsProperties()
    {
        var notif = new RoomUtteranceNotification("Philip", "Hello Iaret", true);

        notif.Speaker.Should().Be("Philip");
        notif.Text.Should().Be("Hello Iaret");
        notif.IsAddressingAgent.Should().BeTrue();
        notif.Source.Should().Be("room");
    }

    [Fact]
    public void SpeakerIdentifiedNotification_SetsProperties()
    {
        var notif = new SpeakerIdentifiedNotification("Philip", true);

        notif.SpeakerLabel.Should().Be("Philip");
        notif.IsOwner.Should().BeTrue();
        notif.Source.Should().Be("voice");
    }

    [Fact]
    public void DeviceEventNotification_SetsProperties()
    {
        var notif = new DeviceEventNotification("Tapo", "camera-1", "motion", "person detected");

        notif.DeviceType.Should().Be("Tapo");
        notif.DeviceId.Should().Be("camera-1");
        notif.EventKind.Should().Be("motion");
        notif.Payload.Should().Be("person detected");
        notif.Source.Should().Be("device:Tapo");
    }

    [Fact]
    public void DeviceEventNotification_NullPayload_IsAllowed()
    {
        var notif = new DeviceEventNotification("sensor", "id", "temp");

        notif.Payload.Should().BeNull();
    }

    [Fact]
    public void ToolStartedNotification_SetsProperties()
    {
        var notif = new ToolStartedNotification("search_my_code", "auth");

        notif.ToolName.Should().Be("search_my_code");
        notif.Parameter.Should().Be("auth");
        notif.Source.Should().Be("tools");
    }

    [Fact]
    public void ToolCompletedNotification_SetsProperties()
    {
        var elapsed = TimeSpan.FromMilliseconds(150);
        var notif = new ToolCompletedNotification("read_file", true, "content", elapsed);

        notif.ToolName.Should().Be("read_file");
        notif.Success.Should().BeTrue();
        notif.Output.Should().Be("content");
        notif.Elapsed.Should().Be(elapsed);
    }

    [Fact]
    public void GoalExecutedNotification_SetsProperties()
    {
        var duration = TimeSpan.FromSeconds(5);
        var notif = new GoalExecutedNotification("Build project", true, duration);

        notif.Goal.Should().Be("Build project");
        notif.Success.Should().BeTrue();
        notif.Duration.Should().Be(duration);
        notif.Source.Should().Be("autonomy");
    }

    [Fact]
    public void LearningCompletedNotification_SetsProperties()
    {
        var notif = new LearningCompletedNotification(10, 3);

        notif.EpisodesProcessed.Should().Be(10);
        notif.RulesLearned.Should().Be(3);
        notif.Source.Should().Be("learning");
    }

    [Fact]
    public void ReasoningCompletedNotification_SetsProperties()
    {
        var notif = new ReasoningCompletedNotification("What is X?", "X is Y", 0.95);

        notif.Query.Should().Be("What is X?");
        notif.Answer.Should().Be("X is Y");
        notif.Confidence.Should().Be(0.95);
        notif.Source.Should().Be("reasoning");
    }

    [Fact]
    public void AllNotifications_ImplementINotification()
    {
        new RoomUtteranceNotification("s", "t", false).Should().BeAssignableTo<INotification>();
        new SpeakerIdentifiedNotification("s", false).Should().BeAssignableTo<INotification>();
        new DeviceEventNotification("t", "i", "k").Should().BeAssignableTo<INotification>();
        new ToolStartedNotification("t", null).Should().BeAssignableTo<INotification>();
        new ToolCompletedNotification("t", true, null, TimeSpan.Zero).Should().BeAssignableTo<INotification>();
        new GoalExecutedNotification("g", true, TimeSpan.Zero).Should().BeAssignableTo<INotification>();
        new LearningCompletedNotification(0, 0).Should().BeAssignableTo<INotification>();
        new ReasoningCompletedNotification("q", "a", 0).Should().BeAssignableTo<INotification>();
    }
}
