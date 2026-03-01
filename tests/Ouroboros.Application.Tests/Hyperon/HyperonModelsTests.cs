using FluentAssertions;
using Ouroboros.Application.Hyperon;
using Xunit;

namespace Ouroboros.Tests.Hyperon;

[Trait("Category", "Unit")]
public class HyperonModelsTests
{
    // --- FlowStepType ---

    [Fact]
    public void FlowStepType_ShouldHave6Values()
    {
        Enum.GetValues<FlowStepType>().Should().HaveCount(6);
    }

    [Fact]
    public void FlowStepType_ShouldContainExpectedValues()
    {
        var values = Enum.GetValues<FlowStepType>();

        values.Should().Contain(FlowStepType.LoadFacts);
        values.Should().Contain(FlowStepType.ApplyRule);
        values.Should().Contain(FlowStepType.Query);
        values.Should().Contain(FlowStepType.Transform);
        values.Should().Contain(FlowStepType.Filter);
        values.Should().Contain(FlowStepType.SideEffect);
    }

    // --- FlowStep ---

    [Fact]
    public void FlowStep_ShouldSetProperties()
    {
        var step = new FlowStep
        {
            StepType = FlowStepType.Query,
            Data = "SELECT * FROM atoms"
        };

        step.StepType.Should().Be(FlowStepType.Query);
        step.Data.Should().Be("SELECT * FROM atoms");
    }

    // --- HyperonFlowEventType ---

    [Fact]
    public void HyperonFlowEventType_ShouldHave5Values()
    {
        Enum.GetValues<HyperonFlowEventType>().Should().HaveCount(5);
    }

    [Fact]
    public void HyperonFlowEventType_ShouldContainExpectedValues()
    {
        var values = Enum.GetValues<HyperonFlowEventType>();

        values.Should().Contain(HyperonFlowEventType.AtomAdded);
        values.Should().Contain(HyperonFlowEventType.PatternMatch);
        values.Should().Contain(HyperonFlowEventType.FlowStarted);
        values.Should().Contain(HyperonFlowEventType.FlowCompleted);
        values.Should().Contain(HyperonFlowEventType.FlowError);
    }

    // --- HyperonFlowEvent ---

    [Fact]
    public void HyperonFlowEvent_ShouldSetRequiredProperties()
    {
        var now = DateTime.UtcNow;
        var evt = new HyperonFlowEvent
        {
            EventType = HyperonFlowEventType.FlowStarted,
            Timestamp = now
        };

        evt.EventType.Should().Be(HyperonFlowEventType.FlowStarted);
        evt.Timestamp.Should().Be(now);
        evt.Data.Should().BeNull();
    }

    [Fact]
    public void HyperonFlowEvent_ShouldSetOptionalData()
    {
        var evt = new HyperonFlowEvent
        {
            EventType = HyperonFlowEventType.FlowError,
            Timestamp = DateTime.UtcNow,
            Data = "Something went wrong"
        };

        evt.Data.Should().Be("Something went wrong");
    }
}
