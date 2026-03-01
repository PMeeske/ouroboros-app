// <copyright file="AdaptiveSpeechDetector.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Concurrent;

namespace Ouroboros.Speech;

/// <summary>
/// Adaptive Voice Activity Detection (VAD) system that learns from ambient noise
/// and adjusts sensitivity dynamically for optimal speech detection.
/// </summary>
public sealed partial class AdaptiveSpeechDetector : IDisposable
{
    // === Configuration ===
    private readonly SpeechDetectionConfig _config;

    // === Adaptive State ===
    private double _ambientNoiseFloor = 0.02;      // Base noise level (RMS)
    private double _currentThreshold = 0.04;       // Dynamic detection threshold
    private double _peakSpeechLevel = 0.3;         // Typical peak speech level
    private readonly CircularBuffer<double> _noiseHistory;
    private readonly CircularBuffer<double> _speechHistory;
    private readonly CircularBuffer<SpeechSegment> _recentSegments;

    // === Detection State ===
    private DateTime _lastSpeechTime = DateTime.MinValue;
    private DateTime _silenceStartTime = DateTime.UtcNow;
    private int _consecutiveSpeechFrames = 0;
    private int _consecutiveSilenceFrames = 0;
    private SpeechState _currentState = SpeechState.Silence;

    // === Self-Voice Exclusion ===
    private volatile bool _isSelfSpeaking = false;
    private DateTime _selfSpeechEndTime = DateTime.MinValue;
    private TimeSpan _selfSpeechCooldown = TimeSpan.FromMilliseconds(500);
    private readonly CircularBuffer<AudioFingerprint> _selfVoiceFingerprints;
    private const int MaxSelfVoiceFingerprints = 50;
    private double _selfVoiceEnergyBaseline = 0.0;
    private double _selfVoiceZcrBaseline = 0.0;
    private int _selfVoiceSamplesCollected = 0;

    // === Statistics ===
    private long _totalFramesAnalyzed = 0;
    private long _speechFramesDetected = 0;
    private readonly ConcurrentDictionary<string, int> _environmentProfile;

    /// <summary>
    /// Current speech detection state.
    /// </summary>
    public enum SpeechState
    {
        /// <summary>No speech detected - ambient silence.</summary>
        Silence,

        /// <summary>Possible speech onset - waiting for confirmation.</summary>
        SpeechOnset,

        /// <summary>Active speech detected.</summary>
        Speaking,

        /// <summary>Speech trailing off - possible end.</summary>
        SpeechOffset,

        /// <summary>Brief pause in speech (not end of utterance).</summary>
        Pause
    }

    /// <summary>
    /// Result of analyzing an audio segment.
    /// </summary>
    /// <param name="HasSpeech">Whether speech was detected.</param>
    /// <param name="Confidence">Confidence level 0.0-1.0.</param>
    /// <param name="EnergyLevel">RMS energy of the segment.</param>
    /// <param name="State">Current detection state.</param>
    /// <param name="IsUtteranceComplete">Whether this marks end of an utterance.</param>
    /// <param name="SuggestedAction">What the caller should do.</param>
    public record SpeechAnalysisResult(
        bool HasSpeech,
        double Confidence,
        double EnergyLevel,
        SpeechState State,
        bool IsUtteranceComplete,
        SuggestedAction SuggestedAction);

    /// <summary>
    /// Suggested action for the caller after analysis.
    /// </summary>
    public enum SuggestedAction
    {
        /// <summary>Continue recording and listening.</summary>
        ContinueListening,

        /// <summary>Process the accumulated audio for transcription.</summary>
        ProcessAudio,

        /// <summary>Discard this audio segment (noise/silence).</summary>
        DiscardSegment,

        /// <summary>Wait for more audio before deciding.</summary>
        WaitForMore
    }

    /// <summary>
    /// Configuration for speech detection behavior.
    /// </summary>
    public record SpeechDetectionConfig(
        double InitialThreshold = 0.04,
        double MinThreshold = 0.015,
        double MaxThreshold = 0.15,
        int SpeechOnsetFrames = 2,
        int SpeechOffsetFrames = 8,
        double AdaptationRate = 0.02,
        int HistorySize = 100,
        double SpeechToNoiseRatio = 2.5,
        bool EnableZeroCrossingRate = true,
        bool EnableSpectralAnalysis = false,
        int SampleRate = 16000);

    /// <summary>
    /// Represents a detected speech segment.
    /// </summary>
    public record SpeechSegment(
        DateTime StartTime,
        DateTime EndTime,
        double PeakEnergy,
        double AverageEnergy,
        int FrameCount);

    /// <summary>
    /// Initializes a new instance of the <see cref="AdaptiveSpeechDetector"/> class.
    /// </summary>
    /// <param name="config">Optional configuration override.</param>
    public AdaptiveSpeechDetector(SpeechDetectionConfig? config = null)
    {
        _config = config ?? new SpeechDetectionConfig();
        _currentThreshold = _config.InitialThreshold;
        _noiseHistory = new CircularBuffer<double>(_config.HistorySize);
        _speechHistory = new CircularBuffer<double>(_config.HistorySize);
        _recentSegments = new CircularBuffer<SpeechSegment>(20);
        _environmentProfile = new ConcurrentDictionary<string, int>();
        _selfVoiceFingerprints = new CircularBuffer<AudioFingerprint>(MaxSelfVoiceFingerprints);
    }

    /// <summary>
    /// Analyzes raw PCM audio data (16-bit, mono) for speech activity.
    /// </summary>
    /// <param name="audioData">Raw PCM audio data.</param>
    /// <returns>Analysis result with speech detection info.</returns>
    public SpeechAnalysisResult AnalyzeAudio(byte[] audioData)
    {
        if (audioData == null || audioData.Length < 64)
        {
            return new SpeechAnalysisResult(
                false, 0.0, 0.0, _currentState, false, SuggestedAction.DiscardSegment);
        }

        // Self-voice exclusion: skip analysis during TTS playback + cooldown
        if (IsSelfVoiceActive())
        {
            return new SpeechAnalysisResult(
                false, 0.0, 0.0, _currentState, false, SuggestedAction.DiscardSegment);
        }

        _totalFramesAnalyzed++;

        // Calculate audio features
        var features = ExtractAudioFeatures(audioData);

        // Check if this matches self-voice fingerprint (echo/feedback detection)
        if (MatchesSelfVoiceFingerprint(features))
        {
            return new SpeechAnalysisResult(
                false, 0.0, features.RmsEnergy, _currentState, false, SuggestedAction.DiscardSegment);
        }

        // Update adaptive thresholds
        UpdateAdaptiveThresholds(features);

        // Determine if this is speech
        bool isSpeech = DetectSpeech(features);
        double confidence = CalculateConfidence(features, isSpeech);

        // Update state machine
        var previousState = _currentState;
        UpdateStateMachine(isSpeech, features);

        // Determine suggested action
        var action = DetermineAction(isSpeech, features);
        bool utteranceComplete = _currentState == SpeechState.Silence &&
                                  previousState is SpeechState.SpeechOffset or SpeechState.Speaking;

        if (utteranceComplete && _consecutiveSpeechFrames > 0)
        {
            RecordSpeechSegment(features);
        }

        return new SpeechAnalysisResult(
            isSpeech,
            confidence,
            features.RmsEnergy,
            _currentState,
            utteranceComplete,
            action);
    }

    /// <summary>
    /// Analyzes a WAV file for speech activity.
    /// </summary>
    /// <param name="wavFilePath">Path to the WAV file.</param>
    /// <returns>Analysis result.</returns>
    public SpeechAnalysisResult AnalyzeWavFile(string wavFilePath)
    {
        if (!File.Exists(wavFilePath))
        {
            return new SpeechAnalysisResult(
                false, 0.0, 0.0, _currentState, false, SuggestedAction.DiscardSegment);
        }

        try
        {
            byte[] fileData = File.ReadAllBytes(wavFilePath);

            // Skip WAV header (typically 44 bytes) to get raw PCM data
            int headerSize = FindWavDataOffset(fileData);
            if (headerSize >= fileData.Length)
            {
                return new SpeechAnalysisResult(
                    false, 0.0, 0.0, _currentState, false, SuggestedAction.DiscardSegment);
            }

            byte[] pcmData = new byte[fileData.Length - headerSize];
            Array.Copy(fileData, headerSize, pcmData, 0, pcmData.Length);

            return AnalyzeAudio(pcmData);
        }
        catch
        {
            return new SpeechAnalysisResult(
                false, 0.0, 0.0, _currentState, false, SuggestedAction.DiscardSegment);
        }
    }

    /// <summary>
    /// Calibrates the detector to the current ambient noise environment.
    /// Should be called during quiet periods to establish baseline.
    /// </summary>
    /// <param name="audioData">Audio sample of ambient noise.</param>
    public void CalibrateToAmbientNoise(byte[] audioData)
    {
        if (audioData == null || audioData.Length < 64) return;

        var features = ExtractAudioFeatures(audioData);

        // Update noise floor with heavy smoothing for calibration
        _ambientNoiseFloor = _ambientNoiseFloor * 0.3 + features.RmsEnergy * 0.7;

        // Set threshold above noise floor
        _currentThreshold = Math.Max(
            _config.MinThreshold,
            _ambientNoiseFloor * _config.SpeechToNoiseRatio);

        _environmentProfile.AddOrUpdate("calibration_count", 1, (_, v) => v + 1);

        Console.WriteLine($"  [🎚️] Calibrated: noise floor={_ambientNoiseFloor:F4}, threshold={_currentThreshold:F4}");
    }

    /// <summary>
    /// Resets the detector state, keeping learned thresholds.
    /// </summary>
    public void ResetState()
    {
        _currentState = SpeechState.Silence;
        _consecutiveSpeechFrames = 0;
        _consecutiveSilenceFrames = 0;
        _silenceStartTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets current detection statistics.
    /// </summary>
    public DetectionStatistics GetStatistics() => new(
        TotalFrames: _totalFramesAnalyzed,
        SpeechFrames: _speechFramesDetected,
        SpeechRatio: _totalFramesAnalyzed > 0 ? (double)_speechFramesDetected / _totalFramesAnalyzed : 0,
        CurrentNoiseFloor: _ambientNoiseFloor,
        CurrentThreshold: _currentThreshold,
        PeakSpeechLevel: _peakSpeechLevel,
        CurrentState: _currentState,
        RecentSegments: _recentSegments.ToArray());

    /// <summary>
    /// Detection statistics record.
    /// </summary>
    public record DetectionStatistics(
        long TotalFrames,
        long SpeechFrames,
        double SpeechRatio,
        double CurrentNoiseFloor,
        double CurrentThreshold,
        double PeakSpeechLevel,
        SpeechState CurrentState,
        SpeechSegment[] RecentSegments);

    /// <summary>
    /// Fingerprint for audio segment comparison.
    /// </summary>
    private record AudioFingerprint(
        double Energy,
        double ZeroCrossingRate,
        double SpectralCentroid,
        double FrequencyRatio,
        DateTime Timestamp);

    /// <inheritdoc/>
    public void Dispose()
    {
        // No unmanaged resources
    }
}
