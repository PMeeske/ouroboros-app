// Copyright (c) Ouroboros. All rights reserved.
using System.Numerics;

namespace Ouroboros.Speech;

/// <summary>
/// FFT-based voice fingerprint detector for TTS self-echo suppression.
///
/// Builds a spectral profile of Iaret's Azure TTS voice by analysing outbound
/// TTS audio with a real Cooley-Tukey FFT, then compares incoming microphone
/// audio against that profile using mel-scale band energies and cosine similarity.
///
/// Call <see cref="RegisterTtsAudio"/> whenever TTS audio is synthesised.
/// Call <see cref="IsTtsEcho"/> on each mic chunk to detect self-echo.
/// </summary>
public sealed class FftVoiceDetector
{
    /// <summary>Number of mel-scale frequency bands used for the spectral envelope.</summary>
    private const int MelBands = 26;

    /// <summary>FFT size — must be power of 2. 1024 ≈ 64 ms at 16 kHz.</summary>
    private const int FftSize = 1024;

    /// <summary>Hop between consecutive FFT frames (50 % overlap).</summary>
    private const int HopSize = FftSize / 2;

    /// <summary>Audio sample rate in Hz.</summary>
    private const int SampleRate = 16000;

    /// <summary>Min mel frequency (Hz) — keep above 80 to skip DC rumble.</summary>
    private const double MelLow = 80;

    /// <summary>Max mel frequency (Hz) — Nyquist / 2 for a 16 kHz signal.</summary>
    private const double MelHigh = 7600;

    // Running average of TTS spectral envelope (mel-band energies).
    private readonly double[] _ttsProfile = new double[MelBands];
    private int _ttsSamples;
    private readonly object _lock = new();

    // Mel filter bank (precomputed once on first use).
    private double[][]? _melFilters;

    /// <summary>
    /// Minimum cosine similarity [0-1] between a mic frame and the TTS profile
    /// to consider it an echo.  Lower = more aggressive suppression.
    /// </summary>
    public double EchoThreshold { get; set; } = 0.82;

    /// <summary>
    /// Minimum number of TTS samples needed before the profile is considered valid.
    /// </summary>
    public int MinProfileSamples { get; set; } = 3;

    /// <summary>Whether a valid TTS profile has been built.</summary>
    public bool HasProfile => _ttsSamples >= MinProfileSamples;

    // ══════════════════════════════════════════════════════════════════
    // PUBLIC API
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Registers outbound TTS audio (raw PCM 16-bit mono 16 kHz) to build
    /// the voice profile.  Call this every time Azure TTS produces audio.
    /// </summary>
    public void RegisterTtsAudio(byte[] pcmData)
    {
        if (pcmData == null || pcmData.Length < FftSize * 2) return;

        var envelope = ComputeAverageMelEnvelope(pcmData);

        lock (_lock)
        {
            _ttsSamples++;
            double alpha = Math.Min(0.4, 1.0 / _ttsSamples);
            for (int i = 0; i < MelBands; i++)
                _ttsProfile[i] = _ttsProfile[i] * (1 - alpha) + envelope[i] * alpha;
        }
    }

    /// <summary>
    /// Tests whether a microphone audio chunk (raw PCM 16-bit mono 16 kHz)
    /// is likely an echo of the TTS voice.
    /// </summary>
    /// <returns>True if the chunk's spectral envelope closely matches the TTS profile.</returns>
    public bool IsTtsEcho(byte[] pcmData)
    {
        if (!HasProfile || pcmData == null || pcmData.Length < FftSize * 2)
            return false;

        var envelope = ComputeAverageMelEnvelope(pcmData);

        double similarity;
        lock (_lock)
        {
            similarity = CosineSimilarity(envelope, _ttsProfile);
        }

        return similarity >= EchoThreshold;
    }

    /// <summary>
    /// Returns the current cosine similarity of a mic chunk against the TTS profile.
    /// Useful for diagnostics / logging.
    /// </summary>
    public double GetSimilarity(byte[] pcmData)
    {
        if (!HasProfile || pcmData == null || pcmData.Length < FftSize * 2)
            return 0;

        var envelope = ComputeAverageMelEnvelope(pcmData);
        lock (_lock)
        {
            return CosineSimilarity(envelope, _ttsProfile);
        }
    }

    /// <summary>Resets the learned TTS profile.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            Array.Clear(_ttsProfile);
            _ttsSamples = 0;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // SPECTRAL FEATURE EXTRACTION
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Computes the average mel-band spectral envelope across all
    /// overlapping FFT frames in the given PCM buffer.
    /// </summary>
    private double[] ComputeAverageMelEnvelope(byte[] pcmData)
    {
        EnsureMelFilters();

        var samples = PcmToDouble(pcmData);
        var envelope = new double[MelBands];
        int frames = 0;

        for (int offset = 0; offset + FftSize <= samples.Length; offset += HopSize)
        {
            // Windowed FFT frame
            var frame = new Complex[FftSize];
            for (int i = 0; i < FftSize; i++)
                frame[i] = new Complex(samples[offset + i] * HannWindow(i, FftSize), 0);

            Fft(frame);

            // Power spectrum (only positive frequencies)
            var power = new double[FftSize / 2 + 1];
            for (int i = 0; i < power.Length; i++)
                power[i] = frame[i].Real * frame[i].Real + frame[i].Imaginary * frame[i].Imaginary;

            // Apply mel filter bank
            for (int b = 0; b < MelBands; b++)
            {
                double sum = 0;
                for (int k = 0; k < power.Length; k++)
                    sum += power[k] * _melFilters![b][k];
                // Log compression (like MFCC step 1)
                envelope[b] += Math.Log(sum + 1e-10);
            }

            frames++;
        }

        if (frames > 0)
        {
            for (int i = 0; i < MelBands; i++)
                envelope[i] /= frames;
        }

        return envelope;
    }

    // ══════════════════════════════════════════════════════════════════
    // COOLEY-TUKEY RADIX-2 FFT (in-place)
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// In-place Cooley-Tukey radix-2 decimation-in-time FFT.
    /// Input length MUST be a power of 2.
    /// </summary>
    private static void Fft(Complex[] data)
    {
        int n = data.Length;

        // Bit-reversal permutation
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            while ((j & bit) != 0)
            {
                j ^= bit;
                bit >>= 1;
            }
            j ^= bit;

            if (i < j)
                (data[i], data[j]) = (data[j], data[i]);
        }

        // Butterfly stages
        for (int len = 2; len <= n; len <<= 1)
        {
            double angle = -2.0 * Math.PI / len;
            var wBase = new Complex(Math.Cos(angle), Math.Sin(angle));

            for (int i = 0; i < n; i += len)
            {
                var w = Complex.One;
                for (int j = 0; j < len / 2; j++)
                {
                    var u = data[i + j];
                    var v = data[i + j + len / 2] * w;
                    data[i + j] = u + v;
                    data[i + j + len / 2] = u - v;
                    w *= wBase;
                }
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // MEL FILTER BANK
    // ══════════════════════════════════════════════════════════════════

    private void EnsureMelFilters()
    {
        if (_melFilters != null) return;

        int bins = FftSize / 2 + 1;
        _melFilters = new double[MelBands][];

        double melLow = HzToMel(MelLow);
        double melHigh = HzToMel(MelHigh);
        var melPoints = new double[MelBands + 2];
        for (int i = 0; i < melPoints.Length; i++)
            melPoints[i] = MelToHz(melLow + (melHigh - melLow) * i / (MelBands + 1));

        // Convert Hz centre frequencies to FFT bin indices
        var binPoints = new int[melPoints.Length];
        for (int i = 0; i < melPoints.Length; i++)
            binPoints[i] = (int)Math.Floor(melPoints[i] / SampleRate * FftSize + 0.5);

        for (int b = 0; b < MelBands; b++)
        {
            _melFilters[b] = new double[bins];
            int left = binPoints[b];
            int centre = binPoints[b + 1];
            int right = binPoints[b + 2];

            for (int k = left; k < centre && k < bins; k++)
            {
                _melFilters[b][k] = (double)(k - left) / Math.Max(1, centre - left);
            }
            for (int k = centre; k <= right && k < bins; k++)
            {
                _melFilters[b][k] = (double)(right - k) / Math.Max(1, right - centre);
            }
        }
    }

    private static double HzToMel(double hz) => 2595.0 * Math.Log10(1 + hz / 700.0);
    private static double MelToHz(double mel) => 700.0 * (Math.Pow(10, mel / 2595.0) - 1);

    // ══════════════════════════════════════════════════════════════════
    // UTILITY
    // ══════════════════════════════════════════════════════════════════

    private static double HannWindow(int i, int n)
        => 0.5 * (1 - Math.Cos(2 * Math.PI * i / (n - 1)));

    private static double[] PcmToDouble(byte[] pcmData)
    {
        int count = pcmData.Length / 2;
        var samples = new double[count];
        for (int i = 0; i < count; i++)
            samples[i] = BitConverter.ToInt16(pcmData, i * 2) / 32768.0;
        return samples;
    }

    private static double CosineSimilarity(double[] a, double[] b)
    {
        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        double denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom < 1e-12 ? 0 : dot / denom;
    }
}
