using FluentAssertions;
using Ouroboros.Application.Personality;
using Xunit;

namespace Ouroboros.Tests.Personality.Consciousness;

[Trait("Category", "Unit")]
public class StimulusTests
{
    [Fact]
    public void CreateNeutral_ShouldSetCorrectType()
    {
        var stimulus = Stimulus.CreateNeutral("greeting", new[] { "hello", "hi" });

        stimulus.Type.Should().Be(StimulusType.Neutral);
        stimulus.Salience.Should().Be(0.5);
        stimulus.Pattern.Should().Be("greeting");
        stimulus.Keywords.Should().HaveCount(2);
        stimulus.EncounterCount.Should().Be(1);
    }

    [Fact]
    public void CreateUnconditioned_ShouldHaveHighSalience()
    {
        var stimulus = Stimulus.CreateUnconditioned("danger", new[] { "error", "crash" });

        stimulus.Type.Should().Be(StimulusType.Unconditioned);
        stimulus.Salience.Should().Be(0.9);
    }

    [Fact]
    public void Matches_WithKeyword_ShouldReturnTrue()
    {
        var stimulus = Stimulus.CreateNeutral("greeting", new[] { "hello", "hi" });

        stimulus.Matches("Hello there!").Should().BeTrue();
    }

    [Fact]
    public void Matches_WithPattern_ShouldReturnTrue()
    {
        var stimulus = Stimulus.CreateNeutral("greeting", new[] { "x" });

        stimulus.Matches("This is a greeting for you").Should().BeTrue();
    }

    [Fact]
    public void Matches_NoMatch_ShouldReturnFalse()
    {
        var stimulus = Stimulus.CreateNeutral("greeting", new[] { "hello", "hi" });

        stimulus.Matches("goodbye").Should().BeFalse();
    }

    [Fact]
    public void CreateNeutral_WithCategory_ShouldSetCategory()
    {
        var stimulus = Stimulus.CreateNeutral("test", new[] { "test" }, "social");

        stimulus.Category.Should().Be("social");
    }
}
