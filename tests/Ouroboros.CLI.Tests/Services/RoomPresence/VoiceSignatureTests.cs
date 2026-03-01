using Ouroboros.CLI.Services.RoomPresence;

namespace Ouroboros.Tests.CLI.Services.RoomPresence;

[Trait("Category", "Unit")]
public class VoiceSignatureTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var sig = new VoiceSignature(0.5, 2000.0, 3.0, 0.6, 5.0);

        sig.RmsEnergy.Should().Be(0.5);
        sig.ZeroCrossRate.Should().Be(2000.0);
        sig.SpeakingRate.Should().Be(3.0);
        sig.DynamicRange.Should().Be(0.6);
        sig.DurationSeconds.Should().Be(5.0);
    }

    [Fact]
    public void FromWavBytes_NullInput_ReturnsNull()
    {
        var result = VoiceSignature.FromWavBytes(null!, 0);

        result.Should().BeNull();
    }

    [Fact]
    public void FromWavBytes_TooShort_ReturnsNull()
    {
        var result = VoiceSignature.FromWavBytes(new byte[10], 0);

        result.Should().BeNull();
    }

    [Fact]
    public void FromWavBytes_InvalidMagicBytes_ReturnsNull()
    {
        var data = new byte[100];
        data[0] = (byte)'X'; // Not RIFF

        var result = VoiceSignature.FromWavBytes(data, 0);

        result.Should().BeNull();
    }

    [Fact]
    public void FromWavBytes_MissingWaveMarker_ReturnsNull()
    {
        var data = new byte[100];
        data[0] = (byte)'R';
        data[1] = (byte)'I';
        data[2] = (byte)'F';
        data[3] = (byte)'F';
        data[8] = (byte)'X'; // Not WAVE

        var result = VoiceSignature.FromWavBytes(data, 0);

        result.Should().BeNull();
    }

    [Fact]
    public void FromWavBytes_ValidWav_ReturnsSignature()
    {
        var wav = CreateMinimalWav(sampleRate: 16000, durationMs: 100);

        var result = VoiceSignature.FromWavBytes(wav, 5);

        result.Should().NotBeNull();
        result!.DurationSeconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SimilarityTo_IdenticalSignatures_ReturnsOne()
    {
        var sig = new VoiceSignature(0.5, 2000.0, 3.0, 0.5, 5.0);

        var similarity = sig.SimilarityTo(sig);

        similarity.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void SimilarityTo_SimilarSignatures_ReturnsHighValue()
    {
        var sig1 = new VoiceSignature(0.5, 2000.0, 3.0, 0.5, 5.0);
        var sig2 = new VoiceSignature(0.6, 2100.0, 3.1, 0.52, 5.0);

        var similarity = sig1.SimilarityTo(sig2);

        similarity.Should().BeGreaterThan(0.95);
    }

    [Fact]
    public void SimilarityTo_VeryDifferentSignatures_ReturnsLowerValue()
    {
        var sig1 = new VoiceSignature(0.1, 500.0, 1.0, 0.1, 3.0);
        var sig2 = new VoiceSignature(0.9, 3500.0, 4.5, 0.9, 5.0);

        var similarity = sig1.SimilarityTo(sig2);

        similarity.Should().BeLessThan(1.0);
        similarity.Should().BeGreaterThanOrEqualTo(0.0);
    }

    [Fact]
    public void SimilarityTo_ZeroVectors_ReturnsZero()
    {
        var sig1 = new VoiceSignature(0, 0, 0, 0, 0);
        var sig2 = new VoiceSignature(0, 0, 0, 0, 0);

        var similarity = sig1.SimilarityTo(sig2);

        similarity.Should().Be(0);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var sig1 = new VoiceSignature(0.5, 2000.0, 3.0, 0.5, 5.0);
        var sig2 = new VoiceSignature(0.5, 2000.0, 3.0, 0.5, 5.0);

        sig1.Should().Be(sig2);
    }

    private static byte[] CreateMinimalWav(int sampleRate, int durationMs)
    {
        int numSamples = sampleRate * durationMs / 1000;
        int dataSize = numSamples * 2; // 16-bit = 2 bytes per sample
        int fileSize = 44 + dataSize;
        var wav = new byte[fileSize];

        // RIFF header
        wav[0] = (byte)'R'; wav[1] = (byte)'I'; wav[2] = (byte)'F'; wav[3] = (byte)'F';
        BitConverter.GetBytes(fileSize - 8).CopyTo(wav, 4);
        wav[8] = (byte)'W'; wav[9] = (byte)'A'; wav[10] = (byte)'V'; wav[11] = (byte)'E';

        // fmt sub-chunk
        wav[12] = (byte)'f'; wav[13] = (byte)'m'; wav[14] = (byte)'t'; wav[15] = (byte)' ';
        BitConverter.GetBytes(16).CopyTo(wav, 16); // Subchunk1Size
        BitConverter.GetBytes((short)1).CopyTo(wav, 20); // AudioFormat (PCM)
        BitConverter.GetBytes((short)1).CopyTo(wav, 22); // NumChannels (mono)
        BitConverter.GetBytes(sampleRate).CopyTo(wav, 24); // SampleRate
        BitConverter.GetBytes(sampleRate * 2).CopyTo(wav, 28); // ByteRate
        BitConverter.GetBytes((short)2).CopyTo(wav, 32); // BlockAlign
        BitConverter.GetBytes((short)16).CopyTo(wav, 34); // BitsPerSample

        // data sub-chunk
        wav[36] = (byte)'d'; wav[37] = (byte)'a'; wav[38] = (byte)'t'; wav[39] = (byte)'a';
        BitConverter.GetBytes(dataSize).CopyTo(wav, 40);

        // Write a simple sine wave
        var rng = new Random(42);
        for (int i = 0; i < numSamples; i++)
        {
            short sample = (short)(Math.Sin(2 * Math.PI * 440 * i / sampleRate) * 10000 + rng.Next(-500, 500));
            BitConverter.GetBytes(sample).CopyTo(wav, 44 + i * 2);
        }

        return wav;
    }
}
