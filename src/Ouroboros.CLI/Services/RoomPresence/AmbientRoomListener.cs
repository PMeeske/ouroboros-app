// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Services.RoomPresence;

using Ouroboros.Abstractions.Monads;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Providers.SpeechToText;
using Ouroboros.Speech;
using Spectre.Console;

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
/// Utterances shorter than <see cref="_minWords"/> words are logged and discarded.
/// </summary>
public sealed class AmbientRoomListener : IAsyncDisposable
{
    private const int ChunkSeconds = 3;

    private readonly ISpeechToTextService _stt;
    private readonly AdaptiveSpeechDetector _vad;
    private readonly FftVoiceDetector _fftDetector = new();
    private readonly int _minWords;
    private CancellationTokenSource? _cts;
    private Task? _captureLoop;
    private bool _disposed;
    private bool _selfSpeaking;

    // ── Diagnostics ──────────────────────────────────────────────────────────
    private int _totalChunks;
    private int _vadDiscards;
    private int _fftEchoDiscards;
    private int _sttFailures;
    private int _wordFilterDiscards;
    private int _recordFailures;
    private DateTime _lastDiagnosticLog = DateTime.MinValue;

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

    /// <summary>
    /// Creates a room-tuned <see cref="AdaptiveSpeechDetector.SpeechDetectionConfig"/>
    /// optimised for far-field ambient listening (lower thresholds, faster onset).
    /// </summary>
    public static AdaptiveSpeechDetector.SpeechDetectionConfig CreateRoomConfig() => new(
        InitialThreshold: 0.025,
        MinThreshold: 0.01,
        SpeechOnsetFrames: 1,
        SpeechOffsetFrames: 5,
        AdaptationRate: 0.02,
        SpeechToNoiseRatio: 1.8,
        EnableZeroCrossingRate: true,
        EnableSpectralAnalysis: false,
        SampleRate: 16000);

    public AmbientRoomListener(
        ISpeechToTextService stt,
        AdaptiveSpeechDetector.SpeechDetectionConfig? vadConfig = null,
        int minWords = 1)
    {
        _stt = stt;
        _vad = new AdaptiveSpeechDetector(vadConfig ?? CreateRoomConfig());
        _minWords = minWords;
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

    /// <summary>
    /// Registers outbound TTS audio (WAV format) with the FFT voice detector
    /// so that subsequent mic chunks matching Iaret's spectral profile are
    /// suppressed as self-echo.  Call this every time Azure TTS produces audio.
    /// The WAV header is stripped automatically.
    /// </summary>
    public void RegisterTtsAudio(byte[] wavData)
    {
        if (wavData == null || wavData.Length < 100) return;
        // Strip WAV header to get raw PCM
        var pcm = wavData.Length > 44 ? wavData[44..] : wavData;
        _fftDetector.RegisterTtsAudio(pcm);
        _vad.RegisterSelfVoiceAudio(pcm);
    }

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
                    await Task.Delay(500, ct).ConfigureAwait(false);
                    continue;
                }

                // Record a 3-second chunk from the microphone
                var recordResult = await MicrophoneRecorder.RecordToMemoryAsync(
                    ChunkSeconds, "wav", ct).ConfigureAwait(false);

                _totalChunks++;

                if (!recordResult.IsSuccess)
                {
                    _recordFailures++;
                    LogDiagnosticsPeriodically();
                    continue;
                }

                var audioBytes = recordResult.Value;

                // VAD pre-filter: skip Whisper call on silent/noise chunks.
                // WAV data starts after the 44-byte header; pass raw PCM to the VAD.
                var pcmForVad = audioBytes.Length > 44 ? audioBytes[44..] : audioBytes;
                var vadResult = _vad.AnalyzeAudio(pcmForVad);
                if (vadResult.SuggestedAction == AdaptiveSpeechDetector.SuggestedAction.DiscardSegment)
                {
                    _vadDiscards++;
                    LogDiagnosticsPeriodically();
                    continue;
                }

                // FFT spectral echo detection: compare mic chunk against Iaret's TTS profile
                if (_fftDetector.IsTtsEcho(pcmForVad))
                {
                    _fftEchoDiscards++;
                    LogDiagnosticsPeriodically();
                    continue;
                }

                // Transcribe the chunk via Whisper (or whichever STT service was injected)
                var transcribeResult = await _stt.TranscribeBytesAsync(
                    audioBytes, "chunk.wav", ct: ct).ConfigureAwait(false);

                if (!transcribeResult.IsSuccess)
                {
                    _sttFailures++;
                    LogDiagnosticsPeriodically();
                    continue;
                }

                var text = transcribeResult.Value.Text.Trim();

                // Filter: Whisper hallucination artifacts on silence/noise
                if (IsWhisperHallucination(text))
                {
                    _wordFilterDiscards++;
                    LogDiagnosticsPeriodically();
                    continue;
                }

                // Filter: at least _minWords meaningful words
                var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (string.IsNullOrWhiteSpace(text) || words.Length < _minWords)
                {
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        _wordFilterDiscards++;
                        AnsiConsole.MarkupLine(OuroborosTheme.Dim($"  [room] Filtered ({words.Length} word{(words.Length == 1 ? "" : "s")}): \"{Markup.Escape(text)}\""));
                    }
                    LogDiagnosticsPeriodically();
                    continue;
                }

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
                AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  [room] Capture error: {Markup.Escape(ex.Message)}"));

                // Brief back-off before retrying
                await Task.Delay(2000, ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Detects common Whisper hallucination artifacts produced on silence or background noise.
    /// </summary>
    private static bool IsWhisperHallucination(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;

        // Exact-match hallucinations (case-insensitive)
        ReadOnlySpan<string> exactPatterns =
        [
            "[BLANK_AUDIO]",
            "(BLANK_AUDIO)",
            "[silence]",
            "(silence)",
            "Thank you.",
            "Thanks for watching.",
            "Thanks for watching!",
            "Thank you for watching.",
            "Thank you for watching!",
            "Please subscribe.",
            "Subscribe to my channel.",
            "Subtitles by the Amara.org community",
            "MoroseTec",
            "ご視聴ありがとうございました",
        ];

        foreach (var pattern in exactPatterns)
        {
            if (text.Equals(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Bracket-wrapped artifacts: [anything], (anything) as sole content
        if ((text.StartsWith('[') && text.EndsWith(']')) ||
            (text.StartsWith('(') && text.EndsWith(')')))
            return true;

        // Repeated single character/word (e.g., "you you you you")
        var trimmedWords = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (trimmedWords.Length >= 3 && trimmedWords.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1)
            return true;

        // Musical note artifacts from background music
        if (text.All(c => c == '♪' || c == '♫' || c == ' ' || c == '.' || c == ','))
            return true;

        return false;
    }

    private void LogDiagnosticsPeriodically()
    {
        if (DateTime.UtcNow - _lastDiagnosticLog < TimeSpan.FromSeconds(60)) return;
        _lastDiagnosticLog = DateTime.UtcNow;
        AnsiConsole.MarkupLine(OuroborosTheme.Dim($"  [room] Audio stats: {_totalChunks} chunks, " +
            $"{_vadDiscards} VAD discards, {_fftEchoDiscards} FFT echo, {_sttFailures} STT fails, " +
            $"{_recordFailures} mic fails, {_wordFilterDiscards} word-filter drops"));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await StopAsync().ConfigureAwait(false);
    }
}
