// <copyright file="EnhancedListeningService.Backends.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Domain.Voice;
using Ouroboros.Providers.SpeechToText;
using Ouroboros.Speech;

namespace Ouroboros.CLI.Services;

/// <summary>
/// STT backends (Azure, Whisper), common processing, wake word, and barge-in handling.
/// </summary>
public sealed partial class EnhancedListeningService
{
    // ════════════════════════════════════════════════════════════════
    // AZURE CONTINUOUS RECOGNITION (Primary Path)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Starts Azure continuous recognition using the built-in mic input.
    /// Azure handles mic capture, VAD, and streaming internally.
    /// We layer wake word detection, barge-in, and state management on top.
    /// </summary>
    private async Task StartAzureContinuousAsync(CancellationToken ct)
    {
        var speechKey = _config.AzureSpeechKey
                        ?? Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
        var speechRegion = Environment.GetEnvironmentVariable("AZURE_SPEECH_REGION")
                           ?? _config.AzureSpeechRegion ?? "eastus";

        if (string.IsNullOrEmpty(speechKey))
        {
            _output.WriteError("Azure Speech key not configured. Set AZURE_SPEECH_KEY or use --azure-speech-key.");
            return;
        }

        var config = SpeechConfig.FromSubscription(speechKey, speechRegion);
        config.SpeechRecognitionLanguage = _config.Culture ?? "en-US";
        config.SetProperty(PropertyId.SpeechServiceResponse_PostProcessingOption, "TrueText");

        // Use default microphone input — Azure handles capture internally
        var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
        _recognizer = new SpeechRecognizer(config, audioConfig);

        // Interim results — show dimmed partial text
        _recognizer.Recognizing += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Result.Text)) return;

            // Publish to interaction stream (triggers state transitions)
            _stream.PublishAudioChunk([], "pcm16", 16000);

            // Show interim text dimmed
            _output.WriteDebug($"  \u2026 {e.Result.Text}");
        };

        // Final results — process the utterance
        _recognizer.Recognized += (_, e) =>
        {
            if (e.Result.Reason != ResultReason.RecognizedSpeech) return;

            var text = e.Result.Text?.Trim();
            if (string.IsNullOrEmpty(text)) return;

            // Publish voice input to interaction stream
            _stream.PublishVoiceInput(text, confidence: 1.0);

            // Process asynchronously
            _ = Task.Run(() => HandleRecognizedTextAsync(text, ct), ct)
                .ContinueWith(t => System.Diagnostics.Debug.WriteLine($"Fire-and-forget fault: {t.Exception}"), TaskContinuationOptions.OnlyOnFaulted);
        };

        _recognizer.Canceled += (_, e) =>
        {
            if (e.Reason == CancellationReason.Error)
            {
                _output.WriteError($"Azure Speech error [{e.ErrorCode}]: {e.ErrorDetails}");
            }
        };

        _recognizer.SessionStarted += (_, _) =>
        {
            _output.WriteDebug("Azure Speech session started");
        };

        await _recognizer.StartContinuousRecognitionAsync();

        _output.WriteSystem(_config.WakeWord != null
            ? $"Listening (say \"{_config.WakeWord}\" to activate)"
            : "Listening (always-on)");

        // Keep alive until cancelled — fire-and-forget to avoid sync-over-async in callback
        ct.Register(() => _ = Task.Run(async () =>
        {
            try { if (_recognizer != null) await _recognizer.StopContinuousRecognitionAsync(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Listening] Stop error: {ex.Message}"); }
        }).ContinueWith(t => System.Diagnostics.Debug.WriteLine($"Fire-and-forget fault: {t.Exception}"), TaskContinuationOptions.OnlyOnFaulted));
    }

    // ════════════════════════════════════════════════════════════════
    // WHISPER FALLBACK PATH
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Starts the Whisper fallback path using Azure SDK mic capture + local Whisper transcription.
    /// Uses Azure AudioConfig just for mic access and PushStream, then transcribes with Whisper.
    /// </summary>
    private async Task StartWhisperFallbackAsync(CancellationToken ct)
    {
        _output.WriteSystem("Initializing Whisper (local) STT...");

        try
        {
            _whisperStt = WhisperNetService.FromModelSize(
                "base",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".whisper"));
        }
        catch (Exception ex)
        {
            _output.WriteError($"Failed to initialize Whisper: {ex.Message}");
            return;
        }

        _output.WriteSystem("Whisper fallback: using segmented recording via system recorder");
        _output.WriteSystem(_config.WakeWord != null
            ? $"Listening (say \"{_config.WakeWord}\" to activate)"
            : "Listening (always-on)");

        // Segmented recording loop: record short segments, transcribe with Whisper
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Record a short segment (5 seconds or until speech ends)
                var recordResult = await MicrophoneRecorder.RecordToMemoryAsync(5, "wav", ct);

                await recordResult.Match(
                    async audioData =>
                    {
                        // Skip near-empty recordings
                        if (audioData.Length < 1000) return;

                        // VAD check
                        var vadResult = _vad.AnalyzeAudio(audioData);
                        if (!vadResult.HasSpeech && vadResult.SuggestedAction == AdaptiveSpeechDetector.SuggestedAction.DiscardSegment)
                            return;

                        // Transcribe with Whisper
                        var transcribeResult = await _whisperStt!.TranscribeBytesAsync(
                            audioData, "recording.wav",
                            new TranscriptionOptions(Language: _config.Culture),
                            ct);

                        transcribeResult.Match(
                            success =>
                            {
                                var text = success.Text?.Trim();
                                if (!string.IsNullOrEmpty(text))
                                {
                                    _stream.PublishVoiceInput(text, confidence: 0.8);
                                    _ = Task.Run(() => HandleRecognizedTextAsync(text, ct), ct)
                                        .ContinueWith(t => System.Diagnostics.Debug.WriteLine($"Fire-and-forget fault: {t.Exception}"), TaskContinuationOptions.OnlyOnFaulted);
                                }
                            },
                            error => _output.WriteDebug($"Whisper error: {error}"));
                    },
                    error =>
                    {
                        if (!ct.IsCancellationRequested)
                            _output.WriteDebug($"Recording error: {error}");
                        return Task.CompletedTask;
                    });
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _output.WriteDebug($"Whisper loop error: {ex.Message}");
                await Task.Delay(1000, ct);
            }
        }
    }

    // ════════════════════════════════════════════════════════════════
    // COMMON PROCESSING
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Handles recognized text: wake word gate, stop commands, agent processing.
    /// </summary>
    private async Task HandleRecognizedTextAsync(string text, CancellationToken ct)
    {
        // Prevent concurrent processing
        if (_isProcessing) return;

        try
        {
            // Check for stop commands
            var lower = text.ToLowerInvariant();
            if (lower.Contains("stop listening") || lower.Contains("disable voice"))
            {
                _output.WriteSystem("Listening stopped by voice command");
                _stream.SendControl(ControlAction.StopListening, "User requested stop");
                return;
            }

            // Wake word gate
            if (!CheckWakeWord(text))
            {
                return;
            }

            // Strip wake word from text if it's at the beginning
            var processedText = StripWakeWord(text);
            if (string.IsNullOrWhiteSpace(processedText))
            {
                _output.WriteSystem("Listening...");
                return;
            }

            _isProcessing = true;
            _presence.TransitionTo(AgentPresenceState.Processing, "Voice input received");

            // Display what user said
            _output.WriteSystem($"  You: {processedText}");

            // Process input
            _currentProcessingCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            string response;

            using (var spinner = _output.StartSpinner("Thinking..."))
            {
                try
                {
                    response = await _processInput(processedText);
                }
                catch (OperationCanceledException)
                {
                    _output.WriteDebug("Processing cancelled (barge-in)");
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(response)) return;

            // Display response
            _output.WriteResponse(_config.Persona, response);

            // Speak response if TTS is enabled
            var speechKey = _config.AzureSpeechKey
                            ?? Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");

            if (_config.AzureTts && !string.IsNullOrEmpty(speechKey))
            {
                _presence.TransitionTo(AgentPresenceState.Speaking, "TTS output started");

                // Notify VAD that self-speech is starting
                _vad.NotifySelfSpeechStarted();

                _currentTtsCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                try
                {
                    await _speak(response, _currentTtsCts.Token);
                }
                catch (OperationCanceledException)
                {
                    _output.WriteDebug("TTS cancelled (barge-in)");
                }
                finally
                {
                    _vad.NotifySelfSpeechEnded();
                    _currentTtsCts?.Dispose();
                    _currentTtsCts = null;
                }
            }

            _presence.TransitionTo(AgentPresenceState.Idle, "Response complete");

            // Reset wake state after timeout (auto-sleep)
            ResetWakeWordTimeout();
        }
        catch (Exception ex)
        {
            _output.WriteDebug($"Processing error: {ex.Message}");
        }
        finally
        {
            _isProcessing = false;
            _currentProcessingCts?.Dispose();
            _currentProcessingCts = null;
        }
    }

    // ════════════════════════════════════════════════════════════════
    // WAKE WORD
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Checks if the recognized text passes the wake word gate.
    /// </summary>
    private bool CheckWakeWord(string text)
    {
        // No wake word configured = always-on
        if (_config.WakeWord == null) return true;

        // Already awake from previous activation
        if (_isAwake) return true;

        // Fuzzy match: check if recognized text contains the wake word
        if (text.Contains(_config.WakeWord, StringComparison.OrdinalIgnoreCase))
        {
            _isAwake = true;
            _output.WriteSystem("Listening...");
            ResetWakeWordTimeout();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Strips the wake word from the beginning of the text.
    /// </summary>
    private string StripWakeWord(string text)
    {
        if (_config.WakeWord == null) return text;

        var idx = text.IndexOf(_config.WakeWord, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var stripped = text[(idx + _config.WakeWord.Length)..].TrimStart(',', '.', '!', '?', ' ');
            return stripped;
        }

        return text;
    }

    /// <summary>
    /// Resets the wake word auto-sleep timeout (2 minutes of silence = go back to sleep).
    /// </summary>
    private void ResetWakeWordTimeout()
    {
        if (_config.WakeWord == null) return; // Always-on, no timeout

        _wakeWordTimeout = Task.Delay(TimeSpan.FromMinutes(2)).ContinueWith(_ =>
        {
            if (_isAwake && !_isProcessing)
            {
                _isAwake = false;
                _output.WriteSystem($"Idle timeout \u2014 say \"{_config.WakeWord}\" to reactivate");
            }
        });
    }

    // ════════════════════════════════════════════════════════════════
    // BARGE-IN
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Handles barge-in events from the AgentPresenceController.
    /// </summary>
    private void OnBargeIn(object? sender, BargeInEventArgs e)
    {
        _output.WriteDebug($"Barge-in: {e.Type}");

        if (e.Type == BargeInType.SpeechInterrupt)
        {
            // Cancel current TTS playback
            _currentTtsCts?.Cancel();
        }
        else if (e.Type == BargeInType.ProcessingCancel)
        {
            // Cancel current LLM generation
            _currentProcessingCts?.Cancel();
        }
    }
}
