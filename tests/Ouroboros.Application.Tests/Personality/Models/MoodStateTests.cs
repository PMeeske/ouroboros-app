using FluentAssertions;
using Ouroboros.Application.Personality;
using Xunit;

namespace Ouroboros.Tests.Personality.Models;

[Trait("Category", "Unit")]
public class MoodStateTests
{
    [Fact]
    public void Neutral_ShouldReturnDefaultMood()
    {
        var mood = MoodState.Neutral;

        mood.Name.Should().Be("neutral");
        mood.Energy.Should().Be(0.5);
        mood.Positivity.Should().Be(0.5);
        mood.TraitModifiers.Should().BeEmpty();
    }

    [Fact]
    public void GetVoiceTone_WithExplicitTone_ShouldReturnTone()
    {
        var mood = new MoodState("excited", 0.9, 0.9,
            new Dictionary<string, double>(), VoiceTone.Excited);

        mood.GetVoiceTone().Should().Be(VoiceTone.Excited);
    }

    [Fact]
    public void GetVoiceTone_WithoutTone_ShouldDeriveFromMoodName()
    {
        var mood = new MoodState("calm", 0.3, 0.6,
            new Dictionary<string, double>());

        var tone = mood.GetVoiceTone();

        tone.Should().Be(VoiceTone.Calm);
    }
}
