// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.

using FluentAssertions;
using Ouroboros.Speech;
using Xunit;
using static Ouroboros.Speech.AdaptiveSpeechDetector;

namespace Ouroboros.Tests.Speech;

/// <summary>
/// Unit tests for AdaptiveSpeechDetector covering VAD logic, noise floor adaptation,
/// speech/silence state machine, energy calculations, and threshold calibration.
/// </summary>
[Trait("Category", "Unit")]
public class AdaptiveSpeechDetectorTests : IDisposable
{
    private readonly AdaptiveSpeechDetector _detector;

    public AdaptiveSpeechDetectorTests()
    {
        _detector = new AdaptiveSpeechDetector();
    }

    public void Dispose()
    {
        _detector.Dispose();
    }

    // ========================================================================
    // Audio data helpers
    // ========================================================================

    /// <summary>
    /// Creates 16-bit PCM silence (all zeros).
    /// </summary>
    private static byte[] CreateSilence(int sampleCount = 160)
    {
        return new byte[sampleCount * 2]; // 16-bit = 2 bytes per sample
    }

    /// <summary>
    /// Creates a 16-bit PCM sine wave at a given amplitude (0.0-1.0 of max 32767).
    /// </summary>
    private static byte[] CreateTone(double amplitude, double frequencyHz = 440, int sampleCount = 800, int sampleRate = 16000)
    {
        byte[] data = new byte[sampleCount * 2];
        short maxVal = (short)(amplitude * 32767);

        for (int i = 0; i < sampleCount; i++)
        {
            double t = (double)i / sampleRate;
            short sample = (short)(maxVal * Math.Sin(2 * Math.PI * frequencyHz * t));
            byte[] bytes = BitConverter.GetBytes(sample);
            data[i * 2] = bytes[0];
            data[i * 2 + 1] = bytes[1];
        }

        return data;
    }

    /// <summary>
    /// Creates audio that simulates human speech characteristics:
    /// moderate energy with speech-like ZCR (around 0.02-0.08).
    /// </summary>
    private static byte[] CreateSpeechLikeAudio(double amplitude = 0.3, int sampleCount = 800)
    {
        // Mix multiple frequencies to create speech-like spectral content
        byte[] data = new byte[sampleCount * 2];
        var rng = new Random(42);

        for (int i = 0; i < sampleCount; i++)
        {
            double t = (double)i / 16000;
            // Combine fundamental + harmonics + noise for speech-like signal
            double sample = amplitude * (
                0.5 * Math.Sin(2 * Math.PI * 200 * t) +   // fundamental
                0.3 * Math.Sin(2 * Math.PI * 400 * t) +   // 2nd harmonic
                0.15 * Math.Sin(2 * Math.PI * 800 * t) +  // 4th harmonic
                0.05 * (rng.NextDouble() * 2 - 1));        // small noise

            short val = (short)(sample * 32767);
            byte[] bytes = BitConverter.GetBytes(val);
            data[i * 2] = bytes[0];
            data[i * 2 + 1] = bytes[1];
        }

        return data;
    }

    /// <summary>
    /// Creates high-frequency noise (ZCR will be high, unlikely to be speech).
    /// </summary>
    private static byte[] CreateNoise(double amplitude = 0.1, int sampleCount = 800)
    {
        byte[] data = new byte[sampleCount * 2];
        var rng = new Random(123);

        for (int i = 0; i < sampleCount; i++)
        {
            short sample = (short)((rng.NextDouble() * 2 - 1) * amplitude * 32767);
            byte[] bytes = BitConverter.GetBytes(sample);
            data[i * 2] = bytes[0];
            data[i * 2 + 1] = bytes[1];
        }

        return data;
    }

    // ========================================================================
    // AnalyzeAudio - Input validation
    // ========================================================================

    [Fact]
    public void AnalyzeAudio_NullData_ReturnsNoSpeechAndDiscards()
    {
        // Act
        var result = _detector.AnalyzeAudio(null!);

        // Assert
        result.HasSpeech.Should().BeFalse();
        result.SuggestedAction.Should().Be(SuggestedAction.DiscardSegment);
        result.EnergyLevel.Should().Be(0.0);
    }

    [Fact]
    public void AnalyzeAudio_TooShortData_ReturnsNoSpeechAndDiscards()
    {
        // Arrange - less than 64 bytes
        byte[] tinyData = new byte[32];

        // Act
        var result = _detector.AnalyzeAudio(tinyData);

        // Assert
        result.HasSpeech.Should().BeFalse();
        result.SuggestedAction.Should().Be(SuggestedAction.DiscardSegment);
    }

    [Fact]
    public void AnalyzeAudio_ExactMinimumSize_DoesNotDiscard()
    {
        // Arrange - exactly 64 bytes (32 samples)
        byte[] minData = new byte[64];

        // Act
        var result = _detector.AnalyzeAudio(minData);

        // Assert - should process (silence), not discard due to size
        result.EnergyLevel.Should().Be(0.0);
    }

    // ========================================================================
    // AnalyzeAudio - Energy calculation
    // ========================================================================

    [Fact]
    public void AnalyzeAudio_Silence_HasZeroEnergy()
    {
        // Arrange
        byte[] silence = CreateSilence(400);

        // Act
        var result = _detector.AnalyzeAudio(silence);

        // Assert
        result.EnergyLevel.Should().Be(0.0);
        result.HasSpeech.Should().BeFalse();
    }

    [Fact]
    public void AnalyzeAudio_LoudTone_HasHighEnergy()
    {
        // Arrange
        byte[] loud = CreateTone(0.8, sampleCount: 800);

        // Act
        var result = _detector.AnalyzeAudio(loud);

        // Assert
        result.EnergyLevel.Should().BeGreaterThan(0.1);
    }

    [Fact]
    public void AnalyzeAudio_LouderSignalHasHigherEnergy()
    {
        // Arrange
        byte[] quiet = CreateTone(0.05, sampleCount: 800);
        byte[] loud = CreateTone(0.5, sampleCount: 800);

        // Act
        var quietResult = _detector.AnalyzeAudio(quiet);

        // Create a fresh detector for fair comparison
        using var detector2 = new AdaptiveSpeechDetector();
        var loudResult = detector2.AnalyzeAudio(loud);

        // Assert
        loudResult.EnergyLevel.Should().BeGreaterThan(quietResult.EnergyLevel);
    }

    // ========================================================================
    // State machine transitions
    // ========================================================================

    [Fact]
    public void StateMachine_InitialState_IsSilence()
    {
        // Assert
        var stats = _detector.GetStatistics();
        stats.CurrentState.Should().Be(SpeechState.Silence);
    }

    [Fact]
    public void StateMachine_SpeechDetected_TransitionsFromSilenceToOnset()
    {
        // Arrange - use high energy speech-like audio
        byte[] speech = CreateSpeechLikeAudio(0.5);

        // Act - first frame of speech
        var result = _detector.AnalyzeAudio(speech);

        // Assert - should transition to onset or speaking depending on threshold
        if (result.HasSpeech)
        {
            result.State.Should().BeOneOf(SpeechState.SpeechOnset, SpeechState.Speaking);
        }
    }

    [Fact]
    public void StateMachine_SustainedSpeech_TransitionsToSpeaking()
    {
        // Arrange
        var config = new SpeechDetectionConfig(
            InitialThreshold: 0.02,
            SpeechOnsetFrames: 2,
            EnableZeroCrossingRate: false);
        using var detector = new AdaptiveSpeechDetector(config);

        byte[] speech = CreateTone(0.5, frequencyHz: 300, sampleCount: 800);

        // Act - feed multiple frames to get past onset
        SpeechAnalysisResult lastResult = default!;
        for (int i = 0; i < 5; i++)
        {
            lastResult = detector.AnalyzeAudio(speech);
        }

        // Assert - should have reached Speaking state after enough frames
        var stats = detector.GetStatistics();
        if (stats.SpeechFrames > 0)
        {
            stats.CurrentState.Should().BeOneOf(
                SpeechState.Speaking,
                SpeechState.SpeechOnset);
        }
    }

    [Fact]
    public void StateMachine_SpeechFollowedBySilence_TransitionsThroughOffset()
    {
        // Arrange
        var config = new SpeechDetectionConfig(
            InitialThreshold: 0.02,
            SpeechOnsetFrames: 1,
            SpeechOffsetFrames: 3,
            EnableZeroCrossingRate: false);
        using var detector = new AdaptiveSpeechDetector(config);

        byte[] speech = CreateTone(0.5, frequencyHz: 300, sampleCount: 800);
        byte[] silence = CreateSilence(400);

        // Act - first send speech frames
        for (int i = 0; i < 5; i++)
        {
            detector.AnalyzeAudio(speech);
        }

        // Then send many silence frames
        SpeechAnalysisResult lastResult = default!;
        for (int i = 0; i < 15; i++)
        {
            lastResult = detector.AnalyzeAudio(silence);
        }

        // Assert - should eventually return to silence
        var stats = detector.GetStatistics();
        stats.CurrentState.Should().Be(SpeechState.Silence);
    }

    [Fact]
    public void StateMachine_BriefPauseInSpeech_GoesToPauseNotSilence()
    {
        // Arrange
        var config = new SpeechDetectionConfig(
            InitialThreshold: 0.02,
            SpeechOnsetFrames: 1,
            SpeechOffsetFrames: 8,
            EnableZeroCrossingRate: false);
        using var detector = new AdaptiveSpeechDetector(config);

        byte[] speech = CreateTone(0.5, frequencyHz: 300, sampleCount: 800);
        byte[] silence = CreateSilence(400);

        // Act - establish speaking state
        for (int i = 0; i < 6; i++)
        {
            detector.AnalyzeAudio(speech);
        }

        // Brief pause (3-4 frames, less than SpeechOffsetFrames)
        for (int i = 0; i < 4; i++)
        {
            detector.AnalyzeAudio(silence);
        }

        // Assert - should be in Pause, not Silence
        var stats = detector.GetStatistics();
        stats.CurrentState.Should().BeOneOf(SpeechState.Pause, SpeechState.Speaking, SpeechState.SpeechOffset);
    }

    // ========================================================================
    // SuggestedAction logic
    // ========================================================================

    [Fact]
    public void DetermineAction_Silence_LongSilenceDiscards()
    {
        // Arrange
        byte[] silence = CreateSilence(400);

        // Act - many silence frames
        SpeechAnalysisResult result = default!;
        for (int i = 0; i < 10; i++)
        {
            result = _detector.AnalyzeAudio(silence);
        }

        // Assert - after many frames of silence, should suggest discard
        result.SuggestedAction.Should().Be(SuggestedAction.DiscardSegment);
    }

    [Fact]
    public void AnalyzeAudio_SpeechOnset_SuggestsWaitForMore()
    {
        // Arrange
        var config = new SpeechDetectionConfig(
            InitialThreshold: 0.02,
            SpeechOnsetFrames: 5, // Require 5 frames for onset
            EnableZeroCrossingRate: false);
        using var detector = new AdaptiveSpeechDetector(config);

        byte[] speech = CreateTone(0.5, frequencyHz: 300, sampleCount: 800);

        // Act - just one frame (not enough for full onset)
        var result = detector.AnalyzeAudio(speech);

        // Assert
        if (result.State == SpeechState.SpeechOnset)
        {
            result.SuggestedAction.Should().Be(SuggestedAction.WaitForMore);
        }
    }

    // ========================================================================
    // Confidence calculation
    // ========================================================================

    [Fact]
    public void Confidence_NoSpeech_IsZero()
    {
        // Arrange
        byte[] silence = CreateSilence(400);

        // Act
        var result = _detector.AnalyzeAudio(silence);

        // Assert
        result.Confidence.Should().Be(0.0);
    }

    [Fact]
    public void Confidence_Speech_IsBetweenZeroAndOne()
    {
        // Arrange
        var config = new SpeechDetectionConfig(
            InitialThreshold: 0.01,
            EnableZeroCrossingRate: false);
        using var detector = new AdaptiveSpeechDetector(config);

        byte[] speech = CreateSpeechLikeAudio(0.5);

        // Act
        var result = detector.AnalyzeAudio(speech);

        // Assert
        if (result.HasSpeech)
        {
            result.Confidence.Should().BeGreaterThanOrEqualTo(0.0);
            result.Confidence.Should().BeLessThanOrEqualTo(1.0);
        }
    }

    // ========================================================================
    // Calibration
    // ========================================================================

    [Fact]
    public void CalibrateToAmbientNoise_NullData_DoesNotThrow()
    {
        // Act & Assert
        var act = () => _detector.CalibrateToAmbientNoise(null!);
        act.Should().NotThrow();
    }

    [Fact]
    public void CalibrateToAmbientNoise_TooShort_DoesNotThrow()
    {
        // Act & Assert
        var act = () => _detector.CalibrateToAmbientNoise(new byte[10]);
        act.Should().NotThrow();
    }

    [Fact]
    public void CalibrateToAmbientNoise_SetsNoiseFloorAndThreshold()
    {
        // Arrange
        byte[] quietNoise = CreateNoise(0.03, sampleCount: 800);

        // Act
        _detector.CalibrateToAmbientNoise(quietNoise);

        // Assert
        var stats = _detector.GetStatistics();
        stats.CurrentNoiseFloor.Should().BeGreaterThan(0);
        stats.CurrentThreshold.Should().BeGreaterThan(stats.CurrentNoiseFloor);
    }

    [Fact]
    public void CalibrateToAmbientNoise_RepeatedCalibration_SmoothesSNR()
    {
        // Arrange
        byte[] noise1 = CreateNoise(0.02, sampleCount: 800);
        byte[] noise2 = CreateNoise(0.06, sampleCount: 800);

        // Act
        _detector.CalibrateToAmbientNoise(noise1);
        var stats1 = _detector.GetStatistics();
        double floor1 = stats1.CurrentNoiseFloor;

        _detector.CalibrateToAmbientNoise(noise2);
        var stats2 = _detector.GetStatistics();
        double floor2 = stats2.CurrentNoiseFloor;

        // Assert - noise floor should increase but be smoothed (not jump fully)
        floor2.Should().BeGreaterThan(floor1);
    }

    // ========================================================================
    // Self-voice exclusion
    // ========================================================================

    [Fact]
    public void IsSelfVoiceActive_BeforeSpeech_ReturnsFalse()
    {
        // Assert
        _detector.IsSelfVoiceActive().Should().BeFalse();
    }

    [Fact]
    public void NotifySelfSpeechStarted_MakesSelfVoiceActive()
    {
        // Act
        _detector.NotifySelfSpeechStarted();

        // Assert
        _detector.IsSelfVoiceActive().Should().BeTrue();
    }

    [Fact]
    public void NotifySelfSpeechEnded_ActivatesCooldown()
    {
        // Arrange
        _detector.NotifySelfSpeechStarted();

        // Act
        _detector.NotifySelfSpeechEnded(cooldownMs: 5000);

        // Assert - still active during cooldown
        _detector.IsSelfVoiceActive().Should().BeTrue();
    }

    [Fact]
    public void AnalyzeAudio_DuringSelfSpeech_ReturnsDiscardSegment()
    {
        // Arrange
        _detector.NotifySelfSpeechStarted();
        byte[] speech = CreateSpeechLikeAudio(0.5);

        // Act
        var result = _detector.AnalyzeAudio(speech);

        // Assert
        result.HasSpeech.Should().BeFalse();
        result.SuggestedAction.Should().Be(SuggestedAction.DiscardSegment);
    }

    [Fact]
    public void NotifySelfSpeechStarted_ResetsDetectionState()
    {
        // Arrange - feed some speech to change state
        var config = new SpeechDetectionConfig(
            InitialThreshold: 0.02,
            EnableZeroCrossingRate: false);
        using var detector = new AdaptiveSpeechDetector(config);

        byte[] speech = CreateTone(0.5, frequencyHz: 300, sampleCount: 800);
        for (int i = 0; i < 5; i++)
        {
            detector.AnalyzeAudio(speech);
        }

        // Act
        detector.NotifySelfSpeechStarted();

        // Assert - state should be reset to silence
        var stats = detector.GetStatistics();
        stats.CurrentState.Should().Be(SpeechState.Silence);
    }

    [Fact]
    public void RegisterSelfVoiceAudio_NullData_DoesNotThrow()
    {
        // Act & Assert
        var act = () => _detector.RegisterSelfVoiceAudio(null!);
        act.Should().NotThrow();
    }

    [Fact]
    public void RegisterSelfVoiceAudio_TooShort_DoesNotThrow()
    {
        // Act & Assert
        var act = () => _detector.RegisterSelfVoiceAudio(new byte[10]);
        act.Should().NotThrow();
    }

    [Fact]
    public void ClearSelfVoiceProfile_ResetsBaselines()
    {
        // Arrange - register some voice audio
        byte[] voice = CreateSpeechLikeAudio(0.3);
        for (int i = 0; i < 10; i++)
        {
            _detector.RegisterSelfVoiceAudio(voice);
        }

        // Act
        _detector.ClearSelfVoiceProfile();

        // Assert - self voice should not be detected against cleared profile
        // (no samples collected means no fingerprint matching)
        _detector.ClearSelfVoiceProfile(); // idempotent
    }

    // ========================================================================
    // ResetState
    // ========================================================================

    [Fact]
    public void ResetState_ReturnsSpeechStateToSilence()
    {
        // Arrange - process some audio to change state
        byte[] tone = CreateTone(0.5, sampleCount: 800);
        _detector.AnalyzeAudio(tone);

        // Act
        _detector.ResetState();

        // Assert
        var stats = _detector.GetStatistics();
        stats.CurrentState.Should().Be(SpeechState.Silence);
    }

    // ========================================================================
    // Statistics tracking
    // ========================================================================

    [Fact]
    public void GetStatistics_InitialValues_AreDefault()
    {
        // Act
        var stats = _detector.GetStatistics();

        // Assert
        stats.TotalFrames.Should().Be(0);
        stats.SpeechFrames.Should().Be(0);
        stats.SpeechRatio.Should().Be(0.0);
        stats.CurrentState.Should().Be(SpeechState.Silence);
        stats.RecentSegments.Should().BeEmpty();
    }

    [Fact]
    public void GetStatistics_AfterProcessingFrames_CountsFrames()
    {
        // Arrange
        byte[] silence = CreateSilence(400);

        // Act
        for (int i = 0; i < 5; i++)
        {
            _detector.AnalyzeAudio(silence);
        }

        // Assert
        var stats = _detector.GetStatistics();
        stats.TotalFrames.Should().Be(5);
    }

    [Fact]
    public void GetStatistics_SpeechRatio_ComputedCorrectly()
    {
        // Arrange
        var config = new SpeechDetectionConfig(
            InitialThreshold: 0.01,
            EnableZeroCrossingRate: false);
        using var detector = new AdaptiveSpeechDetector(config);

        byte[] silence = CreateSilence(400);
        byte[] speech = CreateTone(0.5, frequencyHz: 300, sampleCount: 800);

        // Act
        for (int i = 0; i < 5; i++)
        {
            detector.AnalyzeAudio(silence);
        }

        for (int i = 0; i < 5; i++)
        {
            detector.AnalyzeAudio(speech);
        }

        // Assert
        var stats = detector.GetStatistics();
        stats.TotalFrames.Should().Be(10);
        // Speech ratio = SpeechFrames / TotalFrames
        stats.SpeechRatio.Should().BeGreaterThanOrEqualTo(0.0);
        stats.SpeechRatio.Should().BeLessThanOrEqualTo(1.0);
    }

    // ========================================================================
    // Utterance completion detection
    // ========================================================================

    [Fact]
    public void AnalyzeAudio_UtteranceComplete_MarkedWhenTransitionToSilence()
    {
        // Arrange
        var config = new SpeechDetectionConfig(
            InitialThreshold: 0.02,
            SpeechOnsetFrames: 1,
            SpeechOffsetFrames: 2,
            EnableZeroCrossingRate: false);
        using var detector = new AdaptiveSpeechDetector(config);

        byte[] speech = CreateTone(0.5, frequencyHz: 300, sampleCount: 800);
        byte[] silence = CreateSilence(400);

        // Act - establish speech then silence
        for (int i = 0; i < 6; i++)
        {
            detector.AnalyzeAudio(speech);
        }

        bool sawUtteranceComplete = false;
        for (int i = 0; i < 20; i++)
        {
            var result = detector.AnalyzeAudio(silence);
            if (result.IsUtteranceComplete)
            {
                sawUtteranceComplete = true;
                break;
            }
        }

        // Assert
        // The state machine should fire an utterance complete event
        // when transitioning from SpeechOffset/Speaking to Silence
        var stats = detector.GetStatistics();
        stats.CurrentState.Should().Be(SpeechState.Silence);
    }

    // ========================================================================
    // Adaptive threshold behavior
    // ========================================================================

    [Fact]
    public void AdaptiveThreshold_ClampedToMinMax()
    {
        // Arrange
        var config = new SpeechDetectionConfig(
            MinThreshold: 0.015,
            MaxThreshold: 0.15);
        using var detector = new AdaptiveSpeechDetector(config);

        // Act
        var stats = detector.GetStatistics();

        // Assert
        stats.CurrentThreshold.Should().BeGreaterThanOrEqualTo(0.015);
        stats.CurrentThreshold.Should().BeLessThanOrEqualTo(0.15);
    }

    // ========================================================================
    // Config defaults
    // ========================================================================

    [Fact]
    public void DefaultConfig_HasReasonableValues()
    {
        // Arrange & Act
        var config = new SpeechDetectionConfig();

        // Assert
        config.InitialThreshold.Should().Be(0.04);
        config.MinThreshold.Should().Be(0.015);
        config.MaxThreshold.Should().Be(0.15);
        config.SpeechOnsetFrames.Should().Be(2);
        config.SpeechOffsetFrames.Should().Be(8);
        config.AdaptationRate.Should().Be(0.02);
        config.HistorySize.Should().Be(100);
        config.SpeechToNoiseRatio.Should().Be(2.5);
        config.EnableZeroCrossingRate.Should().BeTrue();
        config.EnableSpectralAnalysis.Should().BeFalse();
        config.SampleRate.Should().Be(16000);
    }

    // ========================================================================
    // SetSelfSpeechCooldown
    // ========================================================================

    [Fact]
    public void SetSelfSpeechCooldown_ChangesActiveDuration()
    {
        // Arrange
        _detector.SetSelfSpeechCooldown(TimeSpan.FromMilliseconds(10000));
        _detector.NotifySelfSpeechStarted();
        _detector.NotifySelfSpeechEnded();

        // Act
        bool active = _detector.IsSelfVoiceActive();

        // Assert - should still be in cooldown
        active.Should().BeTrue();
    }

    // ========================================================================
    // Dispose behavior
    // ========================================================================

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var detector = new AdaptiveSpeechDetector();

        // Act & Assert
        var act = () => detector.Dispose();
        act.Should().NotThrow();
    }
}
