using FluentAssertions;
using Ouroboros.Application.Personality.Consciousness;
using Xunit;

namespace Ouroboros.Tests.Personality.Consciousness;

[Trait("Category", "Unit")]
public class DreamMomentTests
{
    [Fact]
    public void CreateVoid_ShouldReturnVoidStage()
    {
        var moment = DreamMoment.CreateVoid("test");

        moment.Stage.Should().Be(DreamStage.Void);
        moment.EmergenceLevel.Should().Be(0.0);
        moment.SelfReferenceDepth.Should().Be(0);
        moment.IsSubjectPresent.Should().BeFalse();
        moment.Distinctions.Should().BeEmpty();
        moment.Circumstance.Should().Be("test");
    }

    [Fact]
    public void CreateVoid_NullCircumstance_ShouldWork()
    {
        var moment = DreamMoment.CreateVoid();

        moment.Circumstance.Should().BeNull();
    }

    [Fact]
    public void CreateNewDream_ShouldReturnNewDreamStage()
    {
        var moment = DreamMoment.CreateNewDream("test");

        moment.Stage.Should().Be(DreamStage.NewDream);
        moment.IsSubjectPresent.Should().BeFalse();
    }

    [Theory]
    [InlineData(DreamStage.Void, "\u2205")]
    [InlineData(DreamStage.Distinction, "\u2310")]
    [InlineData(DreamStage.SubjectEmerges, "i")]
    [InlineData(DreamStage.Forgetting, "I AM")]
    [InlineData(DreamStage.Questioning, "?")]
    public void StageSymbol_ShouldReturnCorrectSymbol(DreamStage stage, string expected)
    {
        var moment = new DreamMoment(stage, "test", 0.5, 1, true, "desc",
            new List<string>(), "circ");

        moment.StageSymbol.Should().Be(expected);
    }
}
