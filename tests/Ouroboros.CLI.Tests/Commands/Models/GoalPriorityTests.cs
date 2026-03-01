using Ouroboros.CLI.Commands;

namespace Ouroboros.Tests.CLI.Commands.Models;

[Trait("Category", "Unit")]
public class GoalPriorityTests
{
    [Theory]
    [InlineData(GoalPriority.Low)]
    [InlineData(GoalPriority.Normal)]
    [InlineData(GoalPriority.High)]
    [InlineData(GoalPriority.Critical)]
    public void GoalPriority_ContainsExpectedValues(GoalPriority priority)
    {
        Enum.IsDefined(typeof(GoalPriority), priority).Should().BeTrue();
    }

    [Fact]
    public void GoalPriority_HasExactlyFourMembers()
    {
        var values = Enum.GetValues<GoalPriority>();
        values.Should().HaveCount(4);
    }

    [Fact]
    public void GoalPriority_OrderIsCorrect()
    {
        ((int)GoalPriority.Low).Should().BeLessThan((int)GoalPriority.Normal);
        ((int)GoalPriority.Normal).Should().BeLessThan((int)GoalPriority.High);
        ((int)GoalPriority.High).Should().BeLessThan((int)GoalPriority.Critical);
    }
}
