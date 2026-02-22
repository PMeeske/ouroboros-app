// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Services.RoomPresence;

/// <summary>
/// Acoustic fingerprint extracted from a WAV audio chunk.
///
/// Uses simple PCM-level statistics (no ML library required) to characterise
/// a speaker's voice:
///   <see cref="RmsEnergy"/>     — loudness / mic distance proxy
///   <see cref="ZeroCrossRate"/> — zero-crossing rate, correlates with pitch
///   <see cref="SpeakingRate"/>  — words per second (cadence)
///   <see cref="DynamicRange"/>  — amplitude variance (expressiveness)
///
/// Two signatures from the same speaker should have high cosine similarity
/// across the pitch and cadence axes even if recorded at different volumes.
/// </summary>
public sealed record VoiceSignature(
    double RmsEnergy,
    double ZeroCrossRate,
    double SpeakingRate,
    double DynamicRange,
    double DurationSeconds)
{
    /// <summary>
    /// Extracts a <see cref="VoiceSignature"/> from a WAV byte array.
    /// Assumes PCM 16-bit little-endian with a standard 44-byte RIFF header.
    /// Returns null when the audio is too short or malformed.
    /// </summary>
    public static VoiceSignature? FromWavBytes(byte[] wav, int wordCount)
    {
        if (wav == null || wav.Length < 48) return null;

        // Parse WAV header to get sample rate and data offset
        // Standard RIFF/WAVE PCM header is 44 bytes; verify magic bytes
        if (wav[0] != 'R' || wav[1] != 'I' || wav[2] != 'F' || wav[3] != 'F') return null;
        if (wav[8] != 'W' || wav[9] != 'A' || wav[10] != 'V' || wav[11] != 'E') return null;

        int sampleRate     = BitConverter.ToInt32(wav, 24);   // bytes 24-27
        short channels     = BitConverter.ToInt16(wav, 22);   // bytes 22-23
        short bitsPerSample= BitConverter.ToInt16(wav, 34);   // bytes 34-35

        // Find "data" sub-chunk (may not be at exactly byte 36 if fmt chunk is non-standard)
        int dataOffset = 44;
        for (int i = 12; i < wav.Length - 8; i++)
        {
            if (wav[i] == 'd' && wav[i+1] == 'a' && wav[i+2] == 't' && wav[i+3] == 'a')
            {
                dataOffset = i + 8;
                break;
            }
        }

        if (dataOffset >= wav.Length || bitsPerSample != 16 || sampleRate <= 0) return null;

        int sampleCount = (wav.Length - dataOffset) / (2 * Math.Max(1, (int)channels));
        if (sampleCount < 100) return null;

        double durationSeconds = (double)sampleCount / sampleRate;

        // Extract mono samples (use left channel if stereo)
        var samples = new short[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            int byteIdx = dataOffset + i * 2 * channels;
            if (byteIdx + 1 >= wav.Length) break;
            samples[i] = BitConverter.ToInt16(wav, byteIdx);
        }

        // RMS energy
        double sumSq = 0;
        for (int i = 0; i < sampleCount; i++) sumSq += (double)samples[i] * samples[i];
        double rms = Math.Sqrt(sumSq / sampleCount) / 32768.0;

        // Zero-crossing rate (per second)
        int zeroCrossings = 0;
        for (int i = 1; i < sampleCount; i++)
            if ((samples[i] >= 0) != (samples[i - 1] >= 0))
                zeroCrossings++;
        double zeroCrossRate = zeroCrossings / durationSeconds;

        // Dynamic range (normalised)
        short maxAmp = samples[0], minAmp = samples[0];
        for (int i = 1; i < sampleCount; i++)
        {
            if (samples[i] > maxAmp) maxAmp = samples[i];
            if (samples[i] < minAmp) minAmp = samples[i];
        }
        double dynamicRange = (maxAmp - minAmp) / 65536.0;

        // Speaking rate
        double speakingRate = durationSeconds > 0 ? wordCount / durationSeconds : 0;

        return new VoiceSignature(rms, zeroCrossRate, speakingRate, dynamicRange, durationSeconds);
    }

    /// <summary>
    /// Cosine similarity between two voice signatures in the pitch+cadence subspace
    /// (ZeroCrossRate + SpeakingRate + DynamicRange — excludes RmsEnergy which varies
    /// by microphone distance and is not speaker-intrinsic).
    /// Returns a value in [0, 1]. Typical same-speaker similarity: 0.85+.
    /// </summary>
    public double SimilarityTo(VoiceSignature other)
    {
        // Normalise ZCR to [0,1] range (typical range 0–4000 Hz)
        double a1 = ZeroCrossRate  / 4000.0;
        double a2 = SpeakingRate   / 5.0;       // typical 0–5 words/sec
        double a3 = DynamicRange;                // already normalised

        double b1 = other.ZeroCrossRate  / 4000.0;
        double b2 = other.SpeakingRate   / 5.0;
        double b3 = other.DynamicRange;

        double dot   = a1*b1 + a2*b2 + a3*b3;
        double normA = Math.Sqrt(a1*a1 + a2*a2 + a3*a3);
        double normB = Math.Sqrt(b1*b1 + b2*b2 + b3*b3);

        if (normA < 1e-9 || normB < 1e-9) return 0;
        return dot / (normA * normB);
    }
}
