// <copyright file="AdaptiveSpeechDetector.SelfVoice.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Speech;

/// <summary>
/// Self-voice exclusion API and fingerprint matching for echo/feedback detection.
/// </summary>
public sealed partial class AdaptiveSpeechDetector
{
    /// <summary>
    /// Notifies the detector that TTS/self-speech has started.
    /// Audio will be ignored until NotifySelfSpeechEnded is called.
    /// </summary>
    public void NotifySelfSpeechStarted()
    {
        _isSelfSpeaking = true;
        ResetState(); // Clear any pending speech detection
    }

    /// <summary>
    /// Notifies the detector that TTS/self-speech has ended.
    /// A cooldown period will be applied before resuming detection.
    /// </summary>
    /// <param name="cooldownMs">Optional cooldown in milliseconds (default 500ms).</param>
    public void NotifySelfSpeechEnded(int cooldownMs = 500)
    {
        _isSelfSpeaking = false;
        _selfSpeechCooldown = TimeSpan.FromMilliseconds(cooldownMs);
        _selfSpeechEndTime = DateTime.UtcNow;
        ResetState();
    }

    /// <summary>
    /// Registers audio that the system is about to play (TTS output).
    /// Used to create a fingerprint for echo/feedback detection.
    /// </summary>
    /// <param name="ttsAudioData">Raw PCM audio data of TTS output.</param>
    public void RegisterSelfVoiceAudio(byte[] ttsAudioData)
    {
        if (ttsAudioData == null || ttsAudioData.Length < 64) return;

        var features = ExtractAudioFeatures(ttsAudioData);
        var fingerprint = new AudioFingerprint(
            features.RmsEnergy,
            features.ZeroCrossingRate,
            features.SpectralCentroid,
            features.LowFreqEnergy / (features.HighFreqEnergy + 0.001),
            DateTime.UtcNow);

        _selfVoiceFingerprints.Add(fingerprint);

        // Update running baseline of self-voice characteristics
        _selfVoiceSamplesCollected++;
        double alpha = Math.Min(0.3, 1.0 / _selfVoiceSamplesCollected);
        _selfVoiceEnergyBaseline = _selfVoiceEnergyBaseline * (1 - alpha) + features.RmsEnergy * alpha;
        _selfVoiceZcrBaseline = _selfVoiceZcrBaseline * (1 - alpha) + features.ZeroCrossingRate * alpha;
    }

    /// <summary>
    /// Sets the cooldown period after self-speech ends.
    /// </summary>
    /// <param name="cooldown">Cooldown duration.</param>
    public void SetSelfSpeechCooldown(TimeSpan cooldown)
    {
        _selfSpeechCooldown = cooldown;
    }

    /// <summary>
    /// Checks if self-voice exclusion is currently active.
    /// </summary>
    public bool IsSelfVoiceActive()
    {
        if (_isSelfSpeaking) return true;

        // Check cooldown period
        if (_selfSpeechEndTime != DateTime.MinValue)
        {
            var elapsed = DateTime.UtcNow - _selfSpeechEndTime;
            if (elapsed < _selfSpeechCooldown)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Clears all self-voice fingerprints (e.g., when voice profile changes).
    /// </summary>
    public void ClearSelfVoiceProfile()
    {
        _selfVoiceSamplesCollected = 0;
        _selfVoiceEnergyBaseline = 0.0;
        _selfVoiceZcrBaseline = 0.0;
    }

    /// <summary>
    /// Checks if audio features match the learned self-voice profile.
    /// Used to detect echo/feedback from speakers being picked up by mic.
    /// </summary>
    private bool MatchesSelfVoiceFingerprint(AudioFeatures features)
    {
        // Need sufficient samples to have a valid profile
        if (_selfVoiceSamplesCollected < 5) return false;

        // Check if within recent self-speech window (echo detection)
        var recentWindow = TimeSpan.FromSeconds(2);
        var fingerprints = _selfVoiceFingerprints.ToArray()
            .Where(fp => DateTime.UtcNow - fp.Timestamp < recentWindow)
            .ToArray();

        if (fingerprints.Length == 0) return false;

        // Compare current audio to self-voice baseline
        double energyDiff = Math.Abs(features.RmsEnergy - _selfVoiceEnergyBaseline) / (_selfVoiceEnergyBaseline + 0.001);
        double zcrDiff = Math.Abs(features.ZeroCrossingRate - _selfVoiceZcrBaseline) / (_selfVoiceZcrBaseline + 0.001);

        // If very similar to self-voice profile, likely echo
        bool energySimilar = energyDiff < 0.3;
        bool zcrSimilar = zcrDiff < 0.4;

        // Also check against recent fingerprints for direct echo matching
        foreach (var fp in fingerprints)
        {
            double fpEnergyDiff = Math.Abs(features.RmsEnergy - fp.Energy) / (fp.Energy + 0.001);
            double fpZcrDiff = Math.Abs(features.ZeroCrossingRate - fp.ZeroCrossingRate) / (fp.ZeroCrossingRate + 0.001);
            double fpSpectralDiff = Math.Abs(features.SpectralCentroid - fp.SpectralCentroid) / (fp.SpectralCentroid + 0.001);

            // Strong match to a recent self-voice segment
            if (fpEnergyDiff < 0.25 && fpZcrDiff < 0.3 && fpSpectralDiff < 0.35)
            {
                return true;
            }
        }

        // Baseline profile match (for consistent TTS voice)
        return energySimilar && zcrSimilar && _selfVoiceSamplesCollected >= 10;
    }
}
