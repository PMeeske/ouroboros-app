using Ouroboros.CLI.Commands;

namespace Ouroboros.Tests.CLI.Commands.Models;

[Trait("Category", "Unit")]
public class AutonomousThoughtTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var id = Guid.NewGuid();
        var actionType = "Explore";
        var content = "I should investigate this concept";
        var timestamp = DateTime.UtcNow;

        var thought = new AutonomousThought(id, actionType, content, timestamp);

        thought.Id.Should().Be(id);
        thought.ActionType.Should().Be(actionType);
        thought.Content.Should().Be(content);
        thought.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void Equality_TwoIdenticalThoughts_AreEqual()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var thought1 = new AutonomousThought(id, "Think", "content", now);
        var thought2 = new AutonomousThought(id, "Think", "content", now);

        thought1.Should().Be(thought2);
    }

    [Fact]
    public void Equality_DifferentContent_AreNotEqual()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var thought1 = new AutonomousThought(id, "Think", "content1", now);
        var thought2 = new AutonomousThought(id, "Think", "content2", now);

        thought1.Should().NotBe(thought2);
    }
}
