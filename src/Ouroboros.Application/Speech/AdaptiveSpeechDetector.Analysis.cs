// <copyright file="AdaptiveSpeechDetector.Analysis.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Speech;

/// <summary>
/// Audio feature extraction, adaptive threshold management, speech detection algorithms,
/// state machine logic, and the CircularBuffer helper.
/// </summary>
public sealed partial class AdaptiveSpeechDetector
{
    private record AudioFeatures(
        double RmsEnergy,
        double PeakAmplitude,
        double ZeroCrossingRate,
        double LowFreqEnergy,
        double HighFreqEnergy,
        double SpectralCentroid,
        bool HasClipping);

    private AudioFeatures ExtractAudioFeatures(byte[] audioData)
    {
        // Convert bytes to 16-bit samples
        int sampleCount = audioData.Length / 2;
        short[] samples = new short[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] = BitConverter.ToInt16(audioData, i * 2);
        }

        // Calculate RMS energy (normalized)
        double sumSquares = 0;
        double peak = 0;
        int zeroCrossings = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            double normalized = samples[i] / 32768.0;
            sumSquares += normalized * normalized;
            peak = Math.Max(peak, Math.Abs(normalized));

            if (i > 0 && ((samples[i] >= 0) != (samples[i - 1] >= 0)))
            {
                zeroCrossings++;
            }
        }

        double rmsEnergy = Math.Sqrt(sumSquares / sampleCount);
        double zcr = (double)zeroCrossings / sampleCount;

        // Simple frequency band analysis (split signal into low/high components)
        double lowFreqEnergy = 0;
        double highFreqEnergy = 0;

        // Moving average to approximate low frequencies
        int windowSize = Math.Min(32, sampleCount / 4);
        for (int i = windowSize; i < sampleCount; i++)
        {
            double avg = 0;
            for (int j = 0; j < windowSize; j++)
            {
                avg += samples[i - j] / 32768.0;
            }
            avg /= windowSize;

            double original = samples[i] / 32768.0;
            double highFreq = original - avg;

            lowFreqEnergy += avg * avg;
            highFreqEnergy += highFreq * highFreq;
        }

        if (sampleCount > windowSize)
        {
            lowFreqEnergy = Math.Sqrt(lowFreqEnergy / (sampleCount - windowSize));
            highFreqEnergy = Math.Sqrt(highFreqEnergy / (sampleCount - windowSize));
        }

        // Spectral centroid approximation (based on ZCR and energy distribution)
        double spectralCentroid = (zcr * 4000) + (highFreqEnergy / (lowFreqEnergy + 0.001) * 500);

        return new AudioFeatures(
            rmsEnergy,
            peak,
            zcr,
            lowFreqEnergy,
            highFreqEnergy,
            spectralCentroid,
            peak > 0.99);
    }

    private void UpdateAdaptiveThresholds(AudioFeatures features)
    {
        // Slowly adapt to sustained low-energy periods (background noise)
        if (features.RmsEnergy < _currentThreshold * 0.5 && _currentState == SpeechState.Silence)
        {
            _noiseHistory.Add(features.RmsEnergy);

            // Update noise floor with weighted moving average
            if (_noiseHistory.Count >= 10)
            {
                double avgNoise = _noiseHistory.Average();
                _ambientNoiseFloor = _ambientNoiseFloor * (1 - _config.AdaptationRate) +
                                     avgNoise * _config.AdaptationRate;

                // Update threshold to maintain good separation
                double newThreshold = _ambientNoiseFloor * _config.SpeechToNoiseRatio;
                _currentThreshold = Math.Clamp(newThreshold, _config.MinThreshold, _config.MaxThreshold);
            }
        }

        // Track speech energy for peak detection
        if (_currentState == SpeechState.Speaking)
        {
            _speechHistory.Add(features.RmsEnergy);
            if (_speechHistory.Count >= 5)
            {
                double avgSpeech = _speechHistory.Max();
                _peakSpeechLevel = _peakSpeechLevel * 0.95 + avgSpeech * 0.05;
            }
        }
    }

    private bool DetectSpeech(AudioFeatures features)
    {
        // Primary: energy above threshold
        bool energyDetection = features.RmsEnergy > _currentThreshold;

        // Secondary: signal-to-noise ratio check
        double snr = features.RmsEnergy / (_ambientNoiseFloor + 0.001);
        bool snrDetection = snr > _config.SpeechToNoiseRatio * 0.7;

        // Zero crossing rate helps distinguish speech from noise
        // Speech typically has ZCR between 0.01-0.1, pure noise is often higher
        bool zcrInRange = true;
        if (_config.EnableZeroCrossingRate)
        {
            zcrInRange = features.ZeroCrossingRate >= 0.005 && features.ZeroCrossingRate <= 0.15;
        }

        // Spectral characteristics (speech has specific frequency distribution)
        bool spectralMatch = features.SpectralCentroid > 200 && features.SpectralCentroid < 4000;

        // Combined detection with voting
        int votes = 0;
        if (energyDetection) votes += 2;  // Energy is most important
        if (snrDetection) votes += 2;
        if (zcrInRange) votes += 1;
        if (spectralMatch) votes += 1;

        return votes >= 4; // Need at least 4 out of 6 votes
    }

    private double CalculateConfidence(AudioFeatures features, bool isSpeech)
    {
        if (!isSpeech)
        {
            return 0.0;
        }

        double snr = features.RmsEnergy / (_ambientNoiseFloor + 0.001);

        // Confidence based on how far above threshold and typical speech characteristics
        double energyConfidence = Math.Min(1.0, (features.RmsEnergy - _currentThreshold) / _currentThreshold);
        double snrConfidence = Math.Min(1.0, snr / 10.0);

        // ZCR confidence (optimal range for speech is ~0.02-0.08)
        double zcrOptimal = features.ZeroCrossingRate >= 0.02 && features.ZeroCrossingRate <= 0.08 ? 1.0 :
                           features.ZeroCrossingRate >= 0.01 && features.ZeroCrossingRate <= 0.12 ? 0.7 : 0.3;

        return (energyConfidence * 0.4 + snrConfidence * 0.4 + zcrOptimal * 0.2);
    }

    private void UpdateStateMachine(bool isSpeech, AudioFeatures features)
    {
        if (isSpeech)
        {
            _consecutiveSpeechFrames++;
            _consecutiveSilenceFrames = 0;
            _speechFramesDetected++;
        }
        else
        {
            _consecutiveSilenceFrames++;
            // Don't reset speech frames immediately - allows for natural pauses
        }

        _currentState = (_currentState, isSpeech, _consecutiveSpeechFrames, _consecutiveSilenceFrames) switch
        {
            // From Silence
            (SpeechState.Silence, true, >= 1, _) => SpeechState.SpeechOnset,
            (SpeechState.Silence, false, _, _) => SpeechState.Silence,

            // From SpeechOnset
            (SpeechState.SpeechOnset, true, var sf, _) when sf >= _config.SpeechOnsetFrames => SpeechState.Speaking,
            (SpeechState.SpeechOnset, false, _, var silf) when silf >= 2 => SpeechState.Silence,
            (SpeechState.SpeechOnset, _, _, _) => SpeechState.SpeechOnset,

            // From Speaking
            (SpeechState.Speaking, true, _, _) => SpeechState.Speaking,
            (SpeechState.Speaking, false, _, var silf) when silf >= 3 => SpeechState.Pause,
            (SpeechState.Speaking, false, _, _) => SpeechState.Speaking,

            // From Pause (brief pause in speech)
            (SpeechState.Pause, true, _, _) => SpeechState.Speaking,
            (SpeechState.Pause, false, _, var silf) when silf >= _config.SpeechOffsetFrames => SpeechState.SpeechOffset,
            (SpeechState.Pause, false, _, _) => SpeechState.Pause,

            // From SpeechOffset
            (SpeechState.SpeechOffset, true, _, _) => SpeechState.Speaking,
            (SpeechState.SpeechOffset, false, _, var silf) when silf >= _config.SpeechOffsetFrames + 2 => SpeechState.Silence,
            (SpeechState.SpeechOffset, false, _, _) => SpeechState.SpeechOffset,

            // Default
            _ => _currentState
        };

        if (_currentState == SpeechState.Silence)
        {
            _consecutiveSpeechFrames = 0;
            _silenceStartTime = DateTime.UtcNow;
        }
        else if (_currentState is SpeechState.Speaking or SpeechState.SpeechOnset)
        {
            _lastSpeechTime = DateTime.UtcNow;
        }
    }

    private SuggestedAction DetermineAction(bool isSpeech, AudioFeatures features)
    {
        return _currentState switch
        {
            // Silence - either wait or discard
            SpeechState.Silence => _consecutiveSilenceFrames > 5
                ? SuggestedAction.DiscardSegment
                : SuggestedAction.ContinueListening,

            // Onset - wait for confirmation
            SpeechState.SpeechOnset => SuggestedAction.WaitForMore,

            // Speaking - keep recording
            SpeechState.Speaking => SuggestedAction.ContinueListening,

            // Pause - might be mid-sentence
            SpeechState.Pause => SuggestedAction.WaitForMore,

            // Offset - utterance ending, process it
            SpeechState.SpeechOffset => SuggestedAction.ProcessAudio,

            _ => SuggestedAction.ContinueListening
        };
    }

    private void RecordSpeechSegment(AudioFeatures features)
    {
        var segment = new SpeechSegment(
            _lastSpeechTime.AddSeconds(-_consecutiveSpeechFrames * 0.1),
            DateTime.UtcNow,
            features.PeakAmplitude,
            features.RmsEnergy,
            _consecutiveSpeechFrames);

        _recentSegments.Add(segment);
    }

    private static int FindWavDataOffset(byte[] fileData)
    {
        // Look for "data" chunk in WAV header
        for (int i = 0; i < Math.Min(fileData.Length - 4, 200); i++)
        {
            if (fileData[i] == 'd' && fileData[i + 1] == 'a' &&
                fileData[i + 2] == 't' && fileData[i + 3] == 'a')
            {
                return i + 8; // Skip "data" + 4 bytes for size
            }
        }
        return 44; // Default WAV header size
    }

    // === Circular Buffer Implementation ===
    private sealed class CircularBuffer<T>
    {
        private readonly T[] _buffer;
        private int _head;
        private int _count;

        public CircularBuffer(int capacity)
        {
            _buffer = new T[capacity];
        }

        public int Count => _count;

        public void Add(T item)
        {
            _buffer[_head] = item;
            _head = (_head + 1) % _buffer.Length;
            if (_count < _buffer.Length) _count++;
        }

        public double Average()
        {
            if (_count == 0) return 0;
            double sum = 0;
            for (int i = 0; i < _count; i++)
            {
                sum += Convert.ToDouble(_buffer[i]);
            }
            return sum / _count;
        }

        public double Max()
        {
            if (_count == 0) return 0;
            double max = double.MinValue;
            for (int i = 0; i < _count; i++)
            {
                max = Math.Max(max, Convert.ToDouble(_buffer[i]));
            }
            return max;
        }

        public T[] ToArray()
        {
            T[] result = new T[_count];
            for (int i = 0; i < _count; i++)
            {
                int idx = (_head - _count + i + _buffer.Length) % _buffer.Length;
                result[i] = _buffer[idx];
            }
            return result;
        }
    }
}
