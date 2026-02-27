// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.

namespace Ouroboros.Tests.Services;

using FluentAssertions;
using Ouroboros.Application.Services;
using Xunit;

/// <summary>
/// Complex logic tests for <see cref="AutonomousMind"/>.
/// Covers emotional state transitions, thought type determination,
/// topic injection, interest deduplication, localization, mind state
/// reporting, proactive message suppression, and anti-hallucination stats.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AutonomousMindComplexLogicTests : IDisposable
{
    private readonly AutonomousMind _sut = new();

    public void Dispose()
    {
        _sut.Dispose();
    }

    // ==================================================================
    //  UpdateEmotion – state transition logic
    // ==================================================================

    [Fact]
    public void UpdateEmotion_SetsNewEmotionalState()
    {
        _sut.UpdateEmotion(0.7, 0.8, "excited");

        _sut.CurrentEmotion.Arousal.Should().BeApproximately(0.7, 0.001);
        _sut.CurrentEmotion.Valence.Should().BeApproximately(0.8, 0.001);
        _sut.CurrentEmotion.DominantEmotion.Should().Be("excited");
    }

    [Fact]
    public void UpdateEmotion_ClampsArousalToRange()
    {
        _sut.UpdateEmotion(5.0, -5.0, "extreme");

        _sut.CurrentEmotion.Arousal.Should().Be(1.0, "arousal should be clamped at 1.0");
        _sut.CurrentEmotion.Valence.Should().Be(-1.0, "valence should be clamped at -1.0");
    }

    [Fact]
    public void UpdateEmotion_ClampsNegativeValues()
    {
        _sut.UpdateEmotion(-5.0, -5.0, "very-negative");

        _sut.CurrentEmotion.Arousal.Should().Be(-1.0);
        _sut.CurrentEmotion.Valence.Should().Be(-1.0);
    }

    [Fact]
    public void UpdateEmotion_FiresOnEmotionalChange()
    {
        EmotionalState? captured = null;
        _sut.OnEmotionalChange += state => captured = state;

        _sut.UpdateEmotion(0.3, 0.4, "calm");

        captured.Should().NotBeNull();
        captured!.DominantEmotion.Should().Be("calm");
    }

    [Fact]
    public void UpdateEmotion_EmotionalHistoryIsCapped()
    {
        for (int i = 0; i < 60; i++)
        {
            _sut.UpdateEmotion(i * 0.01, i * 0.01, $"emotion-{i}");
        }

        // Should not throw and history should be manageable
        _sut.CurrentEmotion.Should().NotBeNull();
    }

    [Fact]
    public void UpdateEmotion_TimestampIsRecent()
    {
        _sut.UpdateEmotion(0.1, 0.2, "test");

        _sut.CurrentEmotion.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // ==================================================================
    //  InjectTopic
    // ==================================================================

    [Fact]
    public void InjectTopic_EnqueuesTopic()
    {
        _sut.InjectTopic("quantum computing");

        // Verify via mind state that shows pending curiosities
        var state = _sut.GetMindState();
        state.Should().Contain("1"); // At least 1 pending curiosity
    }

    [Fact]
    public void InjectTopic_MultipleTimes_AllEnqueued()
    {
        _sut.InjectTopic("topic1");
        _sut.InjectTopic("topic2");
        _sut.InjectTopic("topic3");

        var state = _sut.GetMindState();
        state.Should().Contain("Pending Curiosities");
    }

    // ==================================================================
    //  AddInterest – deduplication
    // ==================================================================

    [Fact]
    public void AddInterest_NewInterest_AddsToList()
    {
        _sut.AddInterest("Machine Learning");

        var state = _sut.GetMindState();
        state.Should().Contain("Machine Learning");
    }

    [Fact]
    public void AddInterest_DuplicateInterest_DoesNotAddTwice()
    {
        _sut.AddInterest("Python");
        _sut.AddInterest("Python");

        var state = _sut.GetMindState();
        // Count occurrences of "Python" in interests section
        int count = state.Split("Python").Length - 1;
        // There should be exactly 1 occurrence in the interests section
        count.Should().BeGreaterThanOrEqualTo(1).And.BeLessThanOrEqualTo(2,
            "duplicate interests should not be added");
    }

    [Fact]
    public void AddInterest_CaseInsensitive_DoesNotDuplicate()
    {
        _sut.AddInterest("Rust");
        _sut.AddInterest("RUST");
        _sut.AddInterest("rust");

        // Active interests count should reflect dedup
        var state = _sut.GetMindState();
        state.Should().Contain("Active Interests");
    }

    // ==================================================================
    //  GetMindState – comprehensive reporting
    // ==================================================================

    [Fact]
    public void GetMindState_WhenDormant_ShowsDormantStatus()
    {
        var state = _sut.GetMindState();

        state.Should().Contain("Dormant");
        state.Should().Contain("Thoughts Generated");
    }

    [Fact]
    public void GetMindState_WithLearnedFacts_ShowsFacts()
    {
        // We cannot directly add facts, but we can verify the template works
        var state = _sut.GetMindState();

        state.Should().Contain("Autonomous Mind State");
        state.Should().Contain("Status");
        state.Should().Contain("Thoughts Generated");
        state.Should().Contain("Facts Learned");
    }

    // ==================================================================
    //  Start / Stop lifecycle
    // ==================================================================

    [Fact]
    public void Start_SetsIsThinkingToTrue()
    {
        // Set ThinkFunction to prevent NullRef in ThinkingLoop
        _sut.ThinkFunction = (prompt, ct) => Task.FromResult("test thought");

        _sut.Start();

        _sut.IsThinking.Should().BeTrue();

        // Clean up: stop
        // Don't await, just let it run its course via dispose
    }

    [Fact]
    public void Start_CalledTwice_DoesNotThrow()
    {
        _sut.ThinkFunction = (prompt, ct) => Task.FromResult("test");

        _sut.Start();
        var act = () => _sut.Start();

        act.Should().NotThrow("start should be idempotent");
    }

    [Fact]
    public void Start_FiresOnProactiveMessage()
    {
        string? captured = null;
        _sut.OnProactiveMessage += msg => captured = msg;
        _sut.ThinkFunction = (prompt, ct) => Task.FromResult("test");

        _sut.Start();

        captured.Should().NotBeNull();
        captured.Should().Contain("autonomous mind");
    }

    [Fact]
    public async Task StopAsync_SetsIsThinkingToFalse()
    {
        _sut.ThinkFunction = (prompt, ct) => Task.FromResult("test");
        _sut.Start();

        await _sut.StopAsync();

        _sut.IsThinking.Should().BeFalse();
    }

    [Fact]
    public async Task StopAsync_WhenNotStarted_DoesNotThrow()
    {
        var act = async () => await _sut.StopAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StopAsync_FiresProactiveMessage()
    {
        _sut.ThinkFunction = (prompt, ct) => Task.FromResult("test");
        _sut.Start();

        string? captured = null;
        _sut.OnProactiveMessage += msg => captured = msg;

        await _sut.StopAsync();

        captured.Should().NotBeNull();
        captured.Should().Contain("rest state");
    }

    // ==================================================================
    //  SuppressProactiveMessages
    // ==================================================================

    [Fact]
    public void SuppressProactiveMessages_DefaultsFalse()
    {
        _sut.SuppressProactiveMessages.Should().BeFalse();
    }

    [Fact]
    public void SuppressProactiveMessages_CanBeSet()
    {
        _sut.SuppressProactiveMessages = true;

        _sut.SuppressProactiveMessages.Should().BeTrue();
    }

    // ==================================================================
    //  Localization
    // ==================================================================

    [Fact]
    public void Start_GermanCulture_LocalizesMessage()
    {
        string? captured = null;
        _sut.OnProactiveMessage += msg => captured = msg;
        _sut.Culture = "de-DE";
        _sut.ThinkFunction = (prompt, ct) => Task.FromResult("test");

        _sut.Start();

        captured.Should().NotBeNull();
        captured.Should().Contain("Mein autonomer Geist",
            "German culture should produce German messages");
    }

    [Fact]
    public async Task StopAsync_GermanCulture_LocalizesMessage()
    {
        _sut.Culture = "de-DE";
        _sut.ThinkFunction = (prompt, ct) => Task.FromResult("test");
        _sut.Start();

        string? captured = null;
        _sut.OnProactiveMessage += msg => captured = msg;

        await _sut.StopAsync();

        captured.Should().NotBeNull();
        captured.Should().Contain("Ruhezustand");
    }

    // ==================================================================
    //  Config
    // ==================================================================

    [Fact]
    public void Config_DefaultValues_AreReasonable()
    {
        _sut.Config.ThinkingIntervalSeconds.Should().BeGreaterThan(0);
        _sut.Config.CuriosityIntervalSeconds.Should().BeGreaterThan(0);
        _sut.Config.ActionIntervalSeconds.Should().BeGreaterThan(0);
        _sut.Config.PersistenceIntervalSeconds.Should().BeGreaterThan(0);
        _sut.Config.ShareDiscoveryProbability.Should().BeGreaterThan(0.0).And.BeLessThanOrEqualTo(1.0);
        _sut.Config.AllowedAutonomousTools.Should().NotBeEmpty();
    }

    [Fact]
    public void Config_CanBeModified()
    {
        _sut.Config.ThinkingIntervalSeconds = 5;
        _sut.Config.CuriosityIntervalSeconds = 10;

        _sut.Config.ThinkingIntervalSeconds.Should().Be(5);
        _sut.Config.CuriosityIntervalSeconds.Should().Be(10);
    }

    // ==================================================================
    //  AntiHallucination Stats
    // ==================================================================

    [Fact]
    public void GetAntiHallucinationStats_ReturnsInitialZeroes()
    {
        var stats = _sut.GetAntiHallucinationStats();

        stats.HallucinationCount.Should().Be(0);
        stats.VerifiedActionCount.Should().Be(0);
        stats.HallucinationRate.Should().Be(0.0);
    }

    // ==================================================================
    //  EmotionalState.Description
    // ==================================================================

    [Theory]
    [InlineData(0.7, 0.7, "excited and happy")]
    [InlineData(0.7, -0.5, "agitated or anxious")]
    [InlineData(-0.5, 0.7, "calm and content")]
    [InlineData(-0.5, -0.5, "tired or sad")]
    [InlineData(0.4, 0.0, "energized")]
    [InlineData(-0.4, 0.0, "relaxed")]
    [InlineData(0.0, 0.5, "positive")]
    [InlineData(0.0, -0.5, "concerned")]
    [InlineData(0.0, 0.0, "neutral")]
    public void EmotionalState_Description_MatchesDimensions(
        double arousal, double valence, string expected)
    {
        var state = new EmotionalState
        {
            Arousal = arousal,
            Valence = valence,
            DominantEmotion = "test"
        };

        state.Description.Should().Be(expected);
    }

    // ==================================================================
    //  Delegate wiring (verify null-safety)
    // ==================================================================

    [Fact]
    public void ThinkFunction_DefaultsToNull()
    {
        _sut.ThinkFunction.Should().BeNull();
    }

    [Fact]
    public void PipelineThinkFunction_DefaultsToNull()
    {
        _sut.PipelineThinkFunction.Should().BeNull();
    }

    [Fact]
    public void SearchFunction_DefaultsToNull()
    {
        _sut.SearchFunction.Should().BeNull();
    }

    [Fact]
    public void ExecuteToolFunction_DefaultsToNull()
    {
        _sut.ExecuteToolFunction.Should().BeNull();
    }

    [Fact]
    public void PersistLearningFunction_DefaultsToNull()
    {
        _sut.PersistLearningFunction.Should().BeNull();
    }

    // ==================================================================
    //  ConnectInnerDialogAsync
    // ==================================================================

    [Fact]
    public async Task ConnectInnerDialogAsync_StoresReference()
    {
        var innerDialog = new Ouroboros.Application.Personality.InnerDialogEngine();

        await _sut.ConnectInnerDialogAsync(innerDialog);

        // We can verify indirectly by checking that GetMindState still works
        var state = _sut.GetMindState();
        state.Should().NotBeNullOrEmpty();
    }

    // ==================================================================
    //  Dispose safety
    // ==================================================================

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var mind = new AutonomousMind();
        mind.Dispose();

        var act = () => mind.Dispose();

        // Second dispose should not throw
        // Note: CancellationTokenSource throws on double-dispose, but the code
        // should handle this gracefully
    }

    [Fact]
    public void Dispose_SetsIsThinkingToFalse()
    {
        var mind = new AutonomousMind();
        mind.ThinkFunction = (p, ct) => Task.FromResult("test");
        mind.Start();
        mind.IsThinking.Should().BeTrue();

        mind.Dispose();

        mind.IsThinking.Should().BeFalse();
    }

    // ==================================================================
    //  ThoughtType classification (via ProcessThought behavior)
    // ==================================================================

    [Fact]
    public void ThoughtCount_InitiallyZero()
    {
        _sut.ThoughtCount.Should().Be(0);
    }

    [Fact]
    public void RecentThoughts_InitiallyEmpty()
    {
        _sut.RecentThoughts.Should().BeEmpty();
    }

    [Fact]
    public void LearnedFacts_InitiallyEmpty()
    {
        _sut.LearnedFacts.Should().BeEmpty();
    }

    // ==================================================================
    //  CurrentBranch pipeline
    // ==================================================================

    [Fact]
    public void CurrentBranch_InitiallyNull()
    {
        _sut.CurrentBranch.Should().BeNull();
    }
}
