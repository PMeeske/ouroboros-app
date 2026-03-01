using Ouroboros.CLI.Subsystems;

namespace Ouroboros.Tests.CLI.Subsystems;

[Trait("Category", "Unit")]
public class AutonomySubsystemTests
{
    [Fact]
    public void Name_IsAutonomy()
    {
        var sub = new AutonomySubsystem();
        sub.Name.Should().Be("Autonomy");
    }

    [Fact]
    public void IsInitialized_InitiallyFalse()
    {
        var sub = new AutonomySubsystem();
        sub.IsInitialized.Should().BeFalse();
    }

    [Fact]
    public void ImplementsIAutonomySubsystem()
    {
        var sub = new AutonomySubsystem();
        sub.Should().BeAssignableTo<IAutonomySubsystem>();
    }

    [Fact]
    public void ImplementsIAgentSubsystem()
    {
        var sub = new AutonomySubsystem();
        sub.Should().BeAssignableTo<IAgentSubsystem>();
    }

    [Fact]
    public void IsSealed()
    {
        typeof(AutonomySubsystem).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void AutonomousMind_InitiallyNull()
    {
        var sub = new AutonomySubsystem();
        sub.AutonomousMind.Should().BeNull();
    }

    [Fact]
    public void ActionEngine_InitiallyNull()
    {
        var sub = new AutonomySubsystem();
        sub.ActionEngine.Should().BeNull();
    }

    [Fact]
    public void Coordinator_InitiallyNull()
    {
        var sub = new AutonomySubsystem();
        sub.Coordinator.Should().BeNull();
    }

    [Fact]
    public void Orchestrator_InitiallyNull()
    {
        var sub = new AutonomySubsystem();
        sub.Orchestrator.Should().BeNull();
    }

    [Fact]
    public void GoalQueue_InitiallyEmpty()
    {
        var sub = new AutonomySubsystem();
        sub.GoalQueue.Should().BeEmpty();
    }

    [Fact]
    public void SelfExecutionTask_InitiallyNull()
    {
        var sub = new AutonomySubsystem();
        sub.SelfExecutionTask.Should().BeNull();
    }
}
