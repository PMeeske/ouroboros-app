// <copyright file="VoiceModeService.Stt.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reactive.Linq;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Domain.Voice;
using Ouroboros.Providers.SpeechToText;
using Ouroboros.Speech;
using Spectre.Console;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// STT (speech-to-text) methods for VoiceModeService:
/// ListenAsync, GetInputAsync, CheckWhisperAvailable.
/// </summary>
public sealed partial class VoiceModeService
{
    /// <summary>
    /// Listens for voice input using STT.
    /// Uses Rx streaming for presence state and voice input events.
    /// </summary>
    public async Task<string?> ListenAsync(CancellationToken ct = default)
    {
        if (_sttService == null) return null;
        if (_isSpeaking) return null;

        // Set presence state to Listening
        _stream.SetPresenceState(AgentPresenceState.Listening, "Listening for voice input");

        try
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"speech_{Guid.NewGuid()}.wav");

            var recordResult = await MicrophoneRecorder.RecordAsync(
                durationSeconds: 5,
                outputPath: tempFile,
                ct: ct);

            string? audioFile = null;
            recordResult.Match(f => audioFile = f, _ => { });

            if (audioFile != null && File.Exists(audioFile))
            {
                // Set presence state to Processing while transcribing
                _stream.SetPresenceState(AgentPresenceState.Processing, "Transcribing speech");

                try
                {
                    var transcribeResult = await _sttService.TranscribeFileAsync(audioFile, null, ct);
                    string? transcript = null;
                    transcribeResult.Match(t => transcript = t.Text, _ => { });

                    if (!string.IsNullOrWhiteSpace(transcript))
                    {
                        var trimmed = transcript.Trim();

                        // Publish voice input event to Rx stream
                        _stream.PublishVoiceInput(trimmed, confidence: 1.0);
                        _stream.SetPresenceState(AgentPresenceState.Idle, "Voice input received");

                        return trimmed;
                    }
                }
                finally
                {
                    try { File.Delete(audioFile); } catch (IOException) { /* best effort cleanup */ }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _stream.SetPresenceState(AgentPresenceState.Idle, "Listening cancelled");
        }
        catch (Exception ex)
        {
            var face4 = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face4)} ✗ Listen error: {Markup.Escape(ex.Message)}[/]");
            _stream.PublishError(ex.Message, ex, ErrorCategory.SpeechRecognition);
        }

        _stream.SetPresenceState(AgentPresenceState.Idle, "No voice input detected");
        return null;
    }

    /// <summary>
    /// Gets input from either voice or keyboard simultaneously using Rx.
    /// Uses non-blocking keyboard polling and parallel voice recording.
    /// </summary>
    public async Task<string?> GetInputAsync(string prompt = "You: ", CancellationToken ct = default)
    {
        AnsiConsole.Markup(Markup.Escape(prompt));

        // If no STT or no mic, keyboard only
        if (_sttService == null || !MicrophoneRecorder.IsRecordingAvailable())
        {
            return await Task.Run(() => Console.ReadLine(), ct);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var inputBuffer = new System.Text.StringBuilder();
        string? finalResult = null;

        // Keyboard observable - polls Console.KeyAvailable every 50ms
        var keyboardStream = Observable
            .Interval(TimeSpan.FromMilliseconds(50))
            .TakeWhile(_ => !cts.Token.IsCancellationRequested)
            .Select(_ =>
            {
                try
                {
                    while (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(intercept: true); // Intercept to prevent double echo
                        if (key.Key == ConsoleKey.Enter)
                        {
                            Console.WriteLine(); // Move to next line
                            var result = inputBuffer.ToString();
                            inputBuffer.Clear();
                            return result;
                        }
                        else if (key.Key == ConsoleKey.Backspace)
                        {
                            if (inputBuffer.Length > 0)
                            {
                                inputBuffer.Length--;
                                Console.Write("\b \b"); // Erase character visually
                            }
                        }
                        else if (!char.IsControl(key.KeyChar))
                        {
                            inputBuffer.Append(key.KeyChar);
                            Console.Write(key.KeyChar); // Echo character manually
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    // Console redirected - fall through
                }
                return (string?)null;
            })
            .Where(s => s != null)
            .Take(1);

        // Voice observable - starts recording after 500ms delay, gives keyboard priority
        var voiceStream = Observable
            .Timer(TimeSpan.FromMilliseconds(500))
            .SelectMany(_ => Observable.FromAsync(async token =>
            {
                if (cts.Token.IsCancellationRequested) return null;
                try
                {
                    return await ListenAsync(cts.Token);
                }
                catch
                {
                    return null;
                }
            }))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Do(s => AnsiConsole.MarkupLine($"\n  [rgb(148,103,189)]{Markup.Escape("🎤 [" + s + "]")}[/]"))
            .Take(1);

        // Race both streams - first valid input wins
        var resultObservable = keyboardStream
            .Merge(voiceStream)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Take(1)
            .Timeout(TimeSpan.FromMinutes(5))
            .Finally(() =>
            {
                try
                {
                    cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // CTS already disposed - safe to ignore
                }
            });

        try
        {
            finalResult = await resultObservable.FirstOrDefaultAsync();
        }
        catch (TimeoutException)
        {
            finalResult = null;
        }
        catch (InvalidOperationException)
        {
            // Sequence contains no elements - both streams completed without result
            finalResult = null;
        }

        return finalResult;
    }

    private static bool CheckWhisperAvailable()
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "whisper",
                Arguments = "--help",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = System.Diagnostics.Process.Start(startInfo);
            return process != null;
        }
        catch
        {
            return false;
        }
    }
}
