using FluentAssertions;
using Ouroboros.Application.Services;
using Xunit;

namespace Ouroboros.Tests.Services;

[Trait("Category", "Unit")]
public class EmotionalStateTests
{
    [Fact]
    public void Defaults_ShouldBeNeutral()
    {
        var state = new EmotionalState();

        state.Arousal.Should().Be(0.0);
        state.Valence.Should().Be(0.0);
        state.DominantEmotion.Should().Be("neutral");
    }

    [Fact]
    public void Description_HighArousalHighValence_ShouldBeExcitedAndHappy()
    {
        var state = new EmotionalState { Arousal = 0.7, Valence = 0.7 };

        state.Description.Should().Be("excited and happy");
    }

    [Fact]
    public void Description_HighArousalLowValence_ShouldBeAgitated()
    {
        var state = new EmotionalState { Arousal = 0.7, Valence = -0.5 };

        state.Description.Should().Be("agitated or anxious");
    }

    [Fact]
    public void Description_LowArousalHighValence_ShouldBeCalmAndContent()
    {
        var state = new EmotionalState { Arousal = -0.5, Valence = 0.7 };

        state.Description.Should().Be("calm and content");
    }

    [Fact]
    public void Description_LowArousalLowValence_ShouldBeTiredOrSad()
    {
        var state = new EmotionalState { Arousal = -0.5, Valence = -0.5 };

        state.Description.Should().Be("tired or sad");
    }

    [Fact]
    public void Description_NeutralValues_ShouldBeNeutral()
    {
        var state = new EmotionalState { Arousal = 0.0, Valence = 0.0 };

        state.Description.Should().Be("neutral");
    }
}
