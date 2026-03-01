using FluentAssertions;
using Ouroboros.Application.Streams;
using Xunit;

namespace Ouroboros.Tests.Streams;

[Trait("Category", "Unit")]
public class StreamModelsTests
{
    [Fact]
    public void StreamKind_ShouldHave11Values()
    {
        Enum.GetValues<StreamKind>().Should().HaveCount(11);
    }

    [Fact]
    public void StreamKind_ShouldContainExpectedValues()
    {
        var values = Enum.GetValues<StreamKind>();

        values.Should().Contain(StreamKind.Thought);
        values.Should().Contain(StreamKind.Discovery);
        values.Should().Contain(StreamKind.EmotionalChange);
        values.Should().Contain(StreamKind.AutonomousAction);
        values.Should().Contain(StreamKind.ActionEngine);
        values.Should().Contain(StreamKind.InnerDialog);
        values.Should().Contain(StreamKind.ConsciousnessShift);
        values.Should().Contain(StreamKind.ValencePulse);
        values.Should().Contain(StreamKind.PersonalityPulse);
        values.Should().Contain(StreamKind.UserInteraction);
        values.Should().Contain(StreamKind.CoordinatorMessage);
    }
}
