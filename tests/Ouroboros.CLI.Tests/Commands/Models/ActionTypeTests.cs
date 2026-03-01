using Ouroboros.CLI.Commands;

namespace Ouroboros.Tests.CLI.Commands.Models;

[Trait("Category", "Unit")]
public class ActionTypeTests
{
    [Theory]
    [InlineData(ActionType.Chat)]
    [InlineData(ActionType.Help)]
    [InlineData(ActionType.Ask)]
    [InlineData(ActionType.Pipeline)]
    [InlineData(ActionType.Metta)]
    [InlineData(ActionType.Orchestrate)]
    [InlineData(ActionType.Swarm)]
    [InlineData(ActionType.SelfExec)]
    [InlineData(ActionType.SubAgent)]
    [InlineData(ActionType.Dream)]
    [InlineData(ActionType.PromptOptimize)]
    public void ActionType_ContainsExpectedValues(ActionType actionType)
    {
        Enum.IsDefined(typeof(ActionType), actionType).Should().BeTrue();
    }

    [Fact]
    public void ActionType_HasAllExpectedMembers()
    {
        var values = Enum.GetValues<ActionType>();
        values.Should().Contain(ActionType.Chat);
        values.Should().Contain(ActionType.Help);
        values.Should().Contain(ActionType.ListSkills);
        values.Should().Contain(ActionType.ListTools);
        values.Should().Contain(ActionType.LearnTopic);
        values.Should().Contain(ActionType.CreateTool);
        values.Should().Contain(ActionType.UseTool);
        values.Should().Contain(ActionType.RunSkill);
        values.Should().Contain(ActionType.Suggest);
        values.Should().Contain(ActionType.Plan);
        values.Should().Contain(ActionType.Execute);
        values.Should().Contain(ActionType.Status);
        values.Should().Contain(ActionType.Mood);
        values.Should().Contain(ActionType.Remember);
        values.Should().Contain(ActionType.Recall);
        values.Should().Contain(ActionType.Query);
        values.Should().Contain(ActionType.Ask);
        values.Should().Contain(ActionType.Pipeline);
        values.Should().Contain(ActionType.Metta);
        values.Should().Contain(ActionType.Orchestrate);
        values.Should().Contain(ActionType.Network);
        values.Should().Contain(ActionType.Dag);
        values.Should().Contain(ActionType.Affect);
        values.Should().Contain(ActionType.Environment);
        values.Should().Contain(ActionType.Maintenance);
        values.Should().Contain(ActionType.Policy);
        values.Should().Contain(ActionType.Swarm);
    }

    [Fact]
    public void ActionType_CanCastToAndFromInt()
    {
        int chatVal = (int)ActionType.Chat;
        var roundTripped = (ActionType)chatVal;
        roundTripped.Should().Be(ActionType.Chat);
    }
}
