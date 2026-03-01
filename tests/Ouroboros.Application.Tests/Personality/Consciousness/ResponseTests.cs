using FluentAssertions;
using Ouroboros.Application.Personality;
using Xunit;

namespace Ouroboros.Tests.Personality.Consciousness;

[Trait("Category", "Unit")]
public class ResponseTests
{
    [Fact]
    public void CreateEmotional_ShouldSetCorrectType()
    {
        var response = Response.CreateEmotional("warmth", "warm");

        response.Type.Should().Be(ResponseType.Emotional);
        response.Name.Should().Be("warmth");
        response.EmotionalTone.Should().Be("warm");
        response.Intensity.Should().Be(0.7);
        response.Salience.Should().Be(0.5);
    }

    [Fact]
    public void CreateCognitive_ShouldSetCorrectType()
    {
        var response = Response.CreateCognitive("analysis", new[] { "logical", "structured" });

        response.Type.Should().Be(ResponseType.Cognitive);
        response.Name.Should().Be("analysis");
        response.CognitivePatterns.Should().HaveCount(2);
        response.Intensity.Should().Be(0.6);
    }

    [Fact]
    public void CreateEmotional_WithCustomSalience_ShouldOverride()
    {
        var response = Response.CreateEmotional("joy", "positive", 0.9, 0.8);

        response.Intensity.Should().Be(0.9);
        response.Salience.Should().Be(0.8);
    }

    [Fact]
    public void StimulusType_ShouldHave7Values()
    {
        Enum.GetValues<StimulusType>().Should().HaveCount(7);
    }

    [Fact]
    public void ResponseType_ShouldHave6Values()
    {
        Enum.GetValues<ResponseType>().Should().HaveCount(6);
    }
}
