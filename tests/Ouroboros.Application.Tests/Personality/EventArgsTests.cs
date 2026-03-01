using FluentAssertions;
using Ouroboros.Application.Personality;
using Xunit;

namespace Ouroboros.Tests.Personality;

[Trait("Category", "Unit")]
public class EventArgsTests
{
    [Fact]
    public void ConsciousnessShiftEventArgs_ShouldSetProperties()
    {
        var state = ConsciousnessState.Baseline();
        var args = new ConsciousnessShiftEventArgs("excited", 0.3, state);

        args.NewEmotion.Should().Be("excited");
        args.ArousalChange.Should().Be(0.3);
        args.NewState.Should().BeSameAs(state);
    }

    [Fact]
    public void AutonomousThoughtEventArgs_ShouldSetThought()
    {
        var thought = InnerThought.Create(InnerThoughtType.Curiosity, "What is consciousness?");
        var args = new AutonomousThoughtEventArgs(thought);

        args.Thought.Should().BeSameAs(thought);
        args.Thought.Content.Should().Be("What is consciousness?");
    }
}
