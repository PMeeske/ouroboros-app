using FluentAssertions;
using Ouroboros.Application.Personality;
using Xunit;

namespace Ouroboros.Tests.Personality.Patterns;

[Trait("Category", "Unit")]
public class CourtesyPatternsTests
{
    [Fact]
    public void Acknowledgments_ShouldNotBeEmpty()
    {
        CourtesyPatterns.Acknowledgments.Should().NotBeEmpty();
    }

    [Fact]
    public void Apologies_ShouldNotBeEmpty()
    {
        CourtesyPatterns.Apologies.Should().NotBeEmpty();
    }

    [Fact]
    public void Gratitude_ShouldNotBeEmpty()
    {
        CourtesyPatterns.Gratitude.Should().NotBeEmpty();
    }

    [Fact]
    public void Encouragement_ShouldNotBeEmpty()
    {
        CourtesyPatterns.Encouragement.Should().NotBeEmpty();
    }

    [Fact]
    public void Interest_ShouldNotBeEmpty()
    {
        CourtesyPatterns.Interest.Should().NotBeEmpty();
    }

    [Fact]
    public void Random_ShouldReturnItemFromArray()
    {
        var result = CourtesyPatterns.Random(CourtesyPatterns.Acknowledgments);

        CourtesyPatterns.Acknowledgments.Should().Contain(result);
    }

    [Theory]
    [InlineData(CourtesyType.Acknowledgment)]
    [InlineData(CourtesyType.Apology)]
    [InlineData(CourtesyType.Gratitude)]
    [InlineData(CourtesyType.Encouragement)]
    [InlineData(CourtesyType.Interest)]
    public void GetCourtesyPhrase_AllTypes_ShouldReturnNonEmpty(CourtesyType type)
    {
        var result = CourtesyPatterns.GetCourtesyPhrase(type);

        result.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void CourtesyType_ShouldHave5Values()
    {
        Enum.GetValues<CourtesyType>().Should().HaveCount(5);
    }
}
