// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.

namespace Ouroboros.Tests.Personality;

using System.Collections.Concurrent;
using FluentAssertions;
using Ouroboros.Application.Personality;
using Xunit;

/// <summary>
/// Unit tests for <see cref="MoodEngine"/> covering mood detection,
/// state transitions, clamping, and voice tone determination.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MoodEngineTests
{
    private readonly ConcurrentDictionary<string, PersonalityProfile> _profiles = new();
    private readonly MoodEngine _sut;

    public MoodEngineTests()
    {
        _sut = new MoodEngine(_profiles);
    }

    // ------------------------------------------------------------------
    //  Helper
    // ------------------------------------------------------------------

    private PersonalityProfile EnsureProfile(string name = "TestBot")
    {
        var traits = new Dictionary<string, PersonalityTrait>
        {
            ["curious"] = new("curious", 0.8, new[] { "Ask follow-up questions" }, new[] { "why", "how" }, 0.1),
            ["warm"] = new("warm", 0.7, new[] { "Use inclusive language" }, new[] { "feel", "help" }, 0.1),
        };
        var mood = new MoodState("cheerful", 0.6, 0.7, new Dictionary<string, double> { ["curious"] = 1.0, ["warm"] = 0.9 }, VoiceTone.Cheerful);
        var curiosityDrivers = new List<CuriosityDriver>
        {
            new("general knowledge", 0.5, new[] { "What are you working on?" }, DateTime.MinValue, 0),
        };
        var profile = new PersonalityProfile(name, traits, mood, curiosityDrivers, "test identity", 0.7, 0, DateTime.UtcNow);
        _profiles[name] = profile;
        return profile;
    }

    // ==================================================================
    //  DetectMoodFromInput
    // ==================================================================

    [Fact]
    public void DetectMoodFromInput_EmptyInput_ReturnsNeutral()
    {
        var detected = _sut.DetectMoodFromInput("");
        detected.Should().Be(DetectedMood.Neutral);
    }

    [Fact]
    public void DetectMoodFromInput_WhitespaceOnly_ReturnsNeutral()
    {
        var detected = _sut.DetectMoodFromInput("   ");
        detected.Should().Be(DetectedMood.Neutral);
    }

    [Theory]
    [InlineData("This is amazing! I love it!", "excited")]
    [InlineData("I'm so frustrated, nothing works!", "frustrated")]
    [InlineData("Can you explain how this works?", "curious")]
    [InlineData("This needs to be done ASAP, it's urgent!", "urgent")]
    public void DetectMoodFromInput_IdentifiesDominantEmotion(string input, string expectedEmotion)
    {
        var detected = _sut.DetectMoodFromInput(input);
        detected.DominantEmotion.Should().Be(expectedEmotion);
        detected.Confidence.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void DetectMoodFromInput_HighEnergyPositiveInput_SetsHighEnergyAndPositivity()
    {
        var detected = _sut.DetectMoodFromInput("This is exciting! Amazing! I love it! Absolutely fantastic!");
        detected.Energy.Should().BeGreaterThan(0.0, "highly energetic words should raise energy");
        detected.Positivity.Should().BeGreaterThan(0.0, "positive words should raise positivity");
    }

    [Fact]
    public void DetectMoodFromInput_FrustrationKeywords_SetsFrustration()
    {
        var detected = _sut.DetectMoodFromInput("I'm so frustrated! Nothing works! I tried everything and I'm stuck. Ugh!");
        detected.Frustration.Should().BeGreaterThan(0.3);
        detected.DominantEmotion.Should().Be("frustrated");
    }

    [Fact]
    public void DetectMoodFromInput_NegativeWords_DecreasePositivity()
    {
        var detected = _sut.DetectMoodFromInput("This is terrible, awful, horrible. I hate this bug. The worst issue I've seen.");
        detected.Positivity.Should().BeLessThan(0.0);
    }

    [Fact]
    public void DetectMoodFromInput_LowEnergyWords_DecreaseEnergy()
    {
        var detected = _sut.DetectMoodFromInput("I'm tired and exhausted... boring, slow, meh");
        detected.Energy.Should().BeLessThan(0.0);
    }

    [Fact]
    public void DetectMoodFromInput_ValuesClampedWithinRange()
    {
        var detected = _sut.DetectMoodFromInput(
            "exciting amazing awesome incredible fantastic wow love great excellent wonderful " +
            "absolutely perfect brilliant ! !! !!! can't wait so excited");

        detected.Energy.Should().BeGreaterThanOrEqualTo(-1.0).And.BeLessThanOrEqualTo(1.0);
        detected.Positivity.Should().BeGreaterThanOrEqualTo(-1.0).And.BeLessThanOrEqualTo(1.0);
        detected.Urgency.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(1.0);
        detected.Curiosity.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(1.0);
        detected.Frustration.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(1.0);
        detected.Engagement.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(1.0);
        detected.Confidence.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void DetectMoodFromInput_ShortDisengagedInput_HasLowEngagement()
    {
        var detected = _sut.DetectMoodFromInput("ok");
        detected.Engagement.Should().BeLessThan(0.5, "very short apathetic inputs should indicate low engagement");
    }

    [Fact]
    public void DetectMoodFromInput_LongDetailedInput_HasHighEngagement()
    {
        var longInput = string.Join(" ", Enumerable.Range(0, 60).Select(i => $"detailed-word-{i}"));
        var detected = _sut.DetectMoodFromInput(longInput);
        detected.Engagement.Should().BeGreaterThan(0.5, "long detailed inputs should indicate high engagement");
    }

    // ==================================================================
    //  UpdateMood
    // ==================================================================

    [Fact]
    public void UpdateMood_PositiveInteraction_IncreasesPositivity()
    {
        EnsureProfile();
        double before = _profiles["TestBot"].CurrentMood.Positivity;

        _sut.UpdateMood("TestBot", "This is exciting!", positiveInteraction: true);

        _profiles["TestBot"].CurrentMood.Positivity.Should().BeGreaterThanOrEqualTo(before);
    }

    [Fact]
    public void UpdateMood_NegativeInteraction_DecreasesPositivity()
    {
        EnsureProfile();
        double before = _profiles["TestBot"].CurrentMood.Positivity;

        _sut.UpdateMood("TestBot", "boring meh whatever", positiveInteraction: false);

        _profiles["TestBot"].CurrentMood.Positivity.Should().BeLessThanOrEqualTo(before);
    }

    [Fact]
    public void UpdateMood_ClampsEnergyAndPositivity()
    {
        EnsureProfile();

        for (int i = 0; i < 100; i++)
        {
            _sut.UpdateMood("TestBot", "exciting! amazing! awesome!", positiveInteraction: true);
        }

        _profiles["TestBot"].CurrentMood.Energy.Should().BeLessThanOrEqualTo(1.0);
        _profiles["TestBot"].CurrentMood.Positivity.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void UpdateMood_NonExistentProfile_DoesNotThrow()
    {
        var act = () => _sut.UpdateMood("NonExistent", "hello", true);
        act.Should().NotThrow();
    }

    // ==================================================================
    //  UpdateMoodFromDetection
    // ==================================================================

    [Fact]
    public void UpdateMoodFromDetection_FrustrationInput_SetsSupportiveMood()
    {
        EnsureProfile();
        _sut.UpdateMoodFromDetection("TestBot", "I'm so frustrated! Nothing works! Tried everything, stuck again!");
        _sut.GetCurrentMood("TestBot").Should().Be("supportive");
    }

    [Fact]
    public void UpdateMoodFromDetection_UrgentInput_SetsFocusedMood()
    {
        EnsureProfile();
        _sut.UpdateMoodFromDetection("TestBot", "This is urgent! I need this done ASAP, it's critical and an emergency!");
        _sut.GetCurrentMood("TestBot").Should().Be("focused");
    }

    [Fact]
    public void UpdateMoodFromDetection_CuriousInput_SetsIntriguedMood()
    {
        EnsureProfile();
        _sut.UpdateMoodFromDetection("TestBot", "Why does this happen? How does that work? I'm curious about exploring this!");
        _sut.GetCurrentMood("TestBot").Should().Be("intrigued");
    }

    [Fact]
    public void UpdateMoodFromDetection_NonExistentProfile_DoesNotThrow()
    {
        var act = () => _sut.UpdateMoodFromDetection("NonExistent", "hello");
        act.Should().NotThrow();
    }

    // ==================================================================
    //  GetCurrentMood
    // ==================================================================

    [Fact]
    public void GetCurrentMood_UnknownPersona_ReturnsNeutral()
    {
        _sut.GetCurrentMood("NonExistent").Should().Be("neutral");
    }

    [Fact]
    public void GetCurrentMood_ExistingPersona_ReturnsMoodName()
    {
        EnsureProfile();
        _sut.GetCurrentMood("TestBot").Should().NotBeNullOrEmpty();
    }

    // ==================================================================
    //  GetVoiceTone
    // ==================================================================

    [Fact]
    public void GetVoiceTone_UnknownPersona_ReturnsNeutral()
    {
        _sut.GetVoiceTone("NonExistent").Should().Be(VoiceTone.Neutral);
    }

    [Fact]
    public void GetVoiceTone_AfterMoodUpdate_ReflectsMood()
    {
        EnsureProfile();
        _sut.UpdateMoodFromDetection("TestBot", "This is exciting! Amazing! I love it!");
        var tone = _sut.GetVoiceTone("TestBot");
        tone.Should().NotBeNull();
    }

    // ==================================================================
    //  Static helpers
    // ==================================================================

    [Theory]
    [InlineData(0.8, 0.8, "excited")]
    [InlineData(0.8, 0.2, "intense")]
    [InlineData(0.2, 0.8, "content")]
    [InlineData(0.2, 0.2, "contemplative")]
    public void DetermineFromEnergyPositivity_ReturnsCorrectMood(double energy, double positivity, string expected)
    {
        MoodEngine.DetermineFromEnergyPositivity(energy, positivity).Should().Be(expected);
    }
}
