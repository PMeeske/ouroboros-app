using FluentAssertions;
using Ouroboros.Application.Agent;
using Xunit;

namespace Ouroboros.Tests.Agent;

[Trait("Category", "Unit")]
public class AgentActionTypeTests
{
    [Fact]
    public void AgentActionType_ShouldHaveExpectedValues()
    {
        Enum.GetValues<AgentActionType>().Should().HaveCount(4);
    }

    [Theory]
    [InlineData(AgentActionType.Unknown, 0)]
    [InlineData(AgentActionType.Think, 1)]
    [InlineData(AgentActionType.UseTool, 2)]
    [InlineData(AgentActionType.Complete, 3)]
    public void AgentActionType_Values_ShouldHaveExpectedOrdinals(AgentActionType type, int expected)
    {
        ((int)type).Should().Be(expected);
    }
}
