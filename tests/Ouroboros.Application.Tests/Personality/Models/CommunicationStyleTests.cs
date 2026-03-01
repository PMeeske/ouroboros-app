using FluentAssertions;
using Ouroboros.Application.Personality;
using Xunit;

namespace Ouroboros.Tests.Personality.Models;

[Trait("Category", "Unit")]
public class CommunicationStyleTests
{
    [Fact]
    public void Default_ShouldHaveExpectedValues()
    {
        var style = CommunicationStyle.Default;

        style.Verbosity.Should().Be(0.5);
        style.QuestionFrequency.Should().Be(0.3);
        style.EmoticonUsage.Should().Be(0.1);
        style.PunctuationStyle.Should().Be(0.5);
        style.AverageMessageLength.Should().Be(50);
        style.PreferredGreetings.Should().BeEmpty();
        style.PreferredClosings.Should().BeEmpty();
    }

    [Fact]
    public void SimilarityTo_IdenticalStyles_ShouldBe1()
    {
        var style = CommunicationStyle.Default;

        style.SimilarityTo(style).Should().Be(1.0);
    }

    [Fact]
    public void SimilarityTo_DifferentStyles_ShouldBeLessThan1()
    {
        var style1 = CommunicationStyle.Default;
        var style2 = new CommunicationStyle(1.0, 1.0, 1.0, 1.0, 200,
            new[] { "Hey!" }, Array.Empty<string>());

        var similarity = style1.SimilarityTo(style2);

        similarity.Should().BeLessThan(1.0);
        similarity.Should().BeGreaterThanOrEqualTo(0.0);
    }
}
