using Ouroboros.CLI.Commands;

namespace Ouroboros.Tests.CLI.Commands.Models;

[Trait("Category", "Unit")]
public class AutonomousGoalTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var id = Guid.NewGuid();
        var description = "Test goal";
        var priority = GoalPriority.High;
        var createdAt = DateTime.UtcNow;

        var goal = new AutonomousGoal(id, description, priority, createdAt);

        goal.Id.Should().Be(id);
        goal.Description.Should().Be(description);
        goal.Priority.Should().Be(priority);
        goal.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void Equality_TwoIdenticalGoals_AreEqual()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var goal1 = new AutonomousGoal(id, "goal", GoalPriority.Normal, now);
        var goal2 = new AutonomousGoal(id, "goal", GoalPriority.Normal, now);

        goal1.Should().Be(goal2);
    }

    [Fact]
    public void Equality_DifferentPriorities_AreNotEqual()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var goal1 = new AutonomousGoal(id, "goal", GoalPriority.Low, now);
        var goal2 = new AutonomousGoal(id, "goal", GoalPriority.Critical, now);

        goal1.Should().NotBe(goal2);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var goal = new AutonomousGoal(Guid.NewGuid(), "original", GoalPriority.Low, DateTime.UtcNow);

        var modified = goal with { Priority = GoalPriority.Critical };

        modified.Priority.Should().Be(GoalPriority.Critical);
        modified.Description.Should().Be("original");
        goal.Priority.Should().Be(GoalPriority.Low);
    }
}
