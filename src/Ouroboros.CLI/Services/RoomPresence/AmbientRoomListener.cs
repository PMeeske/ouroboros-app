// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Services.RoomPresence;

using Ouroboros.Abstractions.Monads;
using Ouroboros.Providers.SpeechToText;
using Ouroboros.Speech;

/// <summary>
/// A transcribed utterance captured from the ambient microphone.
/// <see cref="Voice"/> carries the acoustic fingerprint extracted from the raw
/// audio bytes and is populated when audio data is available.
/// </summary>
public sealed record RoomUtterance(
    string Text,
    DateTime Timestamp,
    double Confidence,
    string? SpeakerId = null,
    VoiceSignature? Voice = null);

/// <summary>
/// Continuously listens to the room microphone and raises <see cref="OnUtterance"/>
/// for every non-trivial speech segment.
///
/// Uses a 3-second Whisper polling loop (<see cref="MicrophoneRecorder"/>)
/// as the primary STT backend, falling back to nothing if no mic is available.
/// Automatically suppresses Iaret's own TTS output via <see cref="AdaptiveSpeechDetector"/>.
///
/// Utterances shorter than <see cref="MinWords"/> words are silently discarded.
/// </summary>
public sealed class AmbientRoomListener : IAsyncDisposable
{
    private const int ChunkSeconds = 3;
    private const int MinWords = 2;

    private readonly ISpeechToTextService _stt;
    private readonly AdaptiveSpeechDetector _vad;
    private CancellationTokenSource? _cts;
    private Task? _captureLoop;
    private bool _disposed;
    private bool _selfSpeaking;

    /// <summary>Raised for every utterance that passes the word-count filter.</summary>
    public event Action<RoomUtterance>? OnUtterance;

    /// <summary>True while the capture loop is active.</summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Set to <c>true</c> by ImmersiveMode while it is actively recording the user's
    /// voice input. The ambient capture loop will yield the microphone and skip chunks
    /// until this flag is cleared, preventing both modes from recording simultaneously.
    /// </summary>
    public static volatile bool ImmersiveListeningActive;

    public AmbientRoomListener(ISpeechToTextService stt)
    {
        _stt = stt;
        _vad = new AdaptiveSpeechDetector(new AdaptiveSpeechDetector.SpeechDetectionConfig());
    }

    /// <summary>
    /// Starts the ambient capture loop in the background.
    /// Returns immediately; utterances arrive via <see cref="OnUtterance"/>.
    /// </summary>
    public Task StartAsync(CancellationToken ct = default)
    {
        if (IsActive) return Task.CompletedTask;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        IsActive = true;
        _captureLoop = Task.Run(() => CaptureLoopAsync(_cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <summary>Stops the capture loop gracefully.</summary>
    public async Task StopAsync()
    {
        IsActive = false;
        if (_cts is { } cts)
        {
            await cts.CancelAsync();
            cts.Dispose();
            _cts = null;
        }

        if (_captureLoop is { } loop)
        {
            try { await loop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        _captureLoop = null;
    }

    /// <summary>
    /// Notify the VAD that Iaret has started speaking (suppresses self-echo).
    /// </summary>
    public void NotifySelfSpeechStarted() { _selfSpeaking = true; _vad.NotifySelfSpeechStarted(); }

    /// <summary>
    /// Notify the VAD that Iaret's TTS output has finished.
    /// </summary>
    public void NotifySelfSpeechEnded() { _selfSpeaking = false; _vad.NotifySelfSpeechEnded(); }

    // ── Private ──────────────────────────────────────────────────────────────

    private async Task CaptureLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Skip chunk if Iaret is speaking (self-voice suppression) or
                // if ImmersiveMode is actively recording the user's voice (mic mutual exclusion).
                if (_selfSpeaking || ImmersiveListeningActive)
                {
                    await Task.Delay(300, ct).ConfigureAwait(false);
                    continue;
                }

                // Record a 3-second chunk from the microphone
                var recordResult = await MicrophoneRecorder.RecordToMemoryAsync(
                    ChunkSeconds, "wav", ct).ConfigureAwait(false);

                if (!recordResult.IsSuccess)
                    continue;

                var audioBytes = recordResult.Value;

                // VAD pre-filter: skip Whisper call on silent/noise chunks.
                // WAV data starts after the 44-byte header; pass raw PCM to the VAD.
                var pcmForVad = audioBytes.Length > 44 ? audioBytes[44..] : audioBytes;
                var vadResult = _vad.AnalyzeAudio(pcmForVad);
                if (vadResult.SuggestedAction == AdaptiveSpeechDetector.SuggestedAction.DiscardSegment)
                    continue;

                // Transcribe the chunk via Whisper (or whichever STT service was injected)
                var transcribeResult = await _stt.TranscribeBytesAsync(
                    audioBytes, "chunk.wav", ct: ct).ConfigureAwait(false);

                if (!transcribeResult.IsSuccess)
                    continue;

                var text = transcribeResult.Value.Text.Trim();

                // Filter: at least MinWords meaningful words
                var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (string.IsNullOrWhiteSpace(text) || words.Length < MinWords)
                    continue;

                // Extract acoustic fingerprint from the raw WAV bytes
                var voiceSig = VoiceSignature.FromWavBytes(audioBytes, words.Length);

                var utterance = new RoomUtterance(text, DateTime.UtcNow, 1.0, Voice: voiceSig);
                OnUtterance?.Invoke(utterance);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Log to console so the user knows the mic loop is struggling, but keep running
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"  [room] Capture error: {ex.Message}");
                Console.ResetColor();

                // Brief back-off before retrying
                await Task.Delay(2000, ct).ConfigureAwait(false);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await StopAsync().ConfigureAwait(false);
    }
}
