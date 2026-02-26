using FluentAssertions;
using Ouroboros.Application.Personality;
using Xunit;

namespace Ouroboros.Tests.Personality.Models;

[Trait("Category", "Unit")]
public class VoiceToneTests
{
    [Fact]
    public void Neutral_ShouldHaveDefaultValues()
    {
        var tone = VoiceTone.Neutral;

        tone.Rate.Should().Be(0);
        tone.Pitch.Should().Be(0);
        tone.Volume.Should().Be(100);
        tone.Emphasis.Should().BeNull();
        tone.PauseMultiplier.Should().Be(1.0);
    }

    [Fact]
    public void Excited_ShouldBeHighEnergy()
    {
        var tone = VoiceTone.Excited;

        tone.Rate.Should().BeGreaterThan(0);
        tone.Pitch.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Calm_ShouldBeLowEnergy()
    {
        var tone = VoiceTone.Calm;

        tone.Rate.Should().BeLessThan(0);
    }

    [Theory]
    [InlineData("excited")]
    [InlineData("calm")]
    [InlineData("thoughtful")]
    [InlineData("cheerful")]
    [InlineData("focused")]
    [InlineData("warm")]
    public void ForMood_KnownMoods_ShouldReturnSpecificTone(string mood)
    {
        var tone = VoiceTone.ForMood(mood);

        tone.Should().NotBe(VoiceTone.Neutral);
    }

    [Fact]
    public void ForMood_UnknownMood_ShouldReturnNeutral()
    {
        var tone = VoiceTone.ForMood("xyzUnknownMood");

        tone.Should().Be(VoiceTone.Neutral);
    }
}
