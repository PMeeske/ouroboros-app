using FluentAssertions;
using Ouroboros.Application.Personality;
using Xunit;

namespace Ouroboros.Tests.Personality;

[Trait("Category", "Unit")]
public class ConsciousnessStateTests
{
    [Fact]
    public void Baseline_ShouldReturnDefaultState()
    {
        var state = ConsciousnessState.Baseline();

        state.CurrentFocus.Should().Be("awaiting input");
        state.Arousal.Should().Be(0.5);
        state.Valence.Should().Be(0.3);
        state.DominantEmotion.Should().Be("neutral-curious");
        state.Awareness.Should().Be(0.6);
        state.AttentionalSpotlight.Should().BeEmpty();
    }

    [Fact]
    public void Baseline_ActiveDrives_ShouldHaveCuriosityAndSocial()
    {
        var state = ConsciousnessState.Baseline();

        state.ActiveDrives.Should().ContainKey("curiosity");
        state.ActiveDrives.Should().ContainKey("social");
    }

    [Theory]
    [InlineData(0.9, "highly aroused")]
    [InlineData(0.7, "alert")]
    [InlineData(0.5, "calm")]
    [InlineData(0.3, "relaxed")]
    [InlineData(0.1, "drowsy")]
    public void Describe_ArousalLevels_ShouldReturnCorrectDescription(double arousal, string expected)
    {
        var state = new ConsciousnessState("test", arousal, 0.0,
            new Dictionary<string, double>(), new List<string>(),
            "neutral", 0.5, Array.Empty<string>(), DateTime.UtcNow);

        state.Describe().Should().Contain(expected);
    }

    [Theory]
    [InlineData(0.6, "positive")]
    [InlineData(0.3, "slightly positive")]
    [InlineData(0.0, "neutral")]
    [InlineData(-0.3, "slightly negative")]
    [InlineData(-0.6, "negative")]
    public void Describe_ValenceLevels_ShouldReturnCorrectDescription(double valence, string expected)
    {
        var state = new ConsciousnessState("test", 0.5, valence,
            new Dictionary<string, double>(), new List<string>(),
            "neutral", 0.5, Array.Empty<string>(), DateTime.UtcNow);

        state.Describe().Should().Contain(expected);
    }

    [Fact]
    public void Describe_ShouldContainFocusAndEmotion()
    {
        var state = new ConsciousnessState("coding task", 0.5, 0.3,
            new Dictionary<string, double>(), new List<string>(),
            "focused", 0.5, Array.Empty<string>(), DateTime.UtcNow);

        state.Describe().Should().Contain("coding task");
        state.Describe().Should().Contain("focused");
    }
}
