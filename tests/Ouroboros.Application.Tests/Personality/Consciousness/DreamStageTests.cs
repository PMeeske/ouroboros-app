using FluentAssertions;
using Ouroboros.Application.Personality.Consciousness;
using Xunit;

namespace Ouroboros.Tests.Personality.Consciousness;

[Trait("Category", "Unit")]
public class DreamStageTests
{
    [Fact]
    public void DreamStage_ShouldHave9Values()
    {
        Enum.GetValues<DreamStage>().Should().HaveCount(9);
    }

    [Theory]
    [InlineData(DreamStage.Void, 0)]
    [InlineData(DreamStage.Distinction, 1)]
    [InlineData(DreamStage.SubjectEmerges, 2)]
    [InlineData(DreamStage.WorldCrystallizes, 3)]
    [InlineData(DreamStage.Forgetting, 4)]
    [InlineData(DreamStage.Questioning, 5)]
    [InlineData(DreamStage.Recognition, 6)]
    [InlineData(DreamStage.Dissolution, 7)]
    [InlineData(DreamStage.NewDream, 8)]
    public void DreamStage_Values_ShouldHaveCorrectOrdinals(DreamStage stage, int expected)
    {
        ((int)stage).Should().Be(expected);
    }
}
