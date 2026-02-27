// <copyright file="VoiceModeService.Tts.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reactive.Linq;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Domain.Voice;
using Ouroboros.Abstractions.Monads;
using Ouroboros.Providers.TextToSpeech;
using Ouroboros.Speech;
using Spectre.Console;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// TTS (text-to-speech) methods for VoiceModeService:
/// SayAsync, WhisperAsync, SpeakInternalAsync, SpeakWithLocalTtsAsync,
/// SpeakWithCloudTtsAsync, SanitizeForTts, SplitIntoSemanticChunks.
/// </summary>
public sealed partial class VoiceModeService
{
    /// <summary>
    /// Speaks text using TTS with console output.
    /// Uses Rx-based SpeechQueue for proper serialization with VoiceSideChannel.
    /// </summary>
    public async Task SayAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // If voice mode is not initialized, just print text (no TTS)
        if (!_isInitialized)
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("[>]")} {OuroborosTheme.Accent(_persona.Name + ":")} {Markup.Escape(text)}");
            return;
        }

        // Sanitize for TTS
        string sanitized = SanitizeForTts(text);
        if (string.IsNullOrWhiteSpace(sanitized)) return;

        // Initialize SpeechQueue with our TTS
        Ouroboros.Domain.Autonomous.SpeechQueue.Instance.SetSynthesizer(async (t, p, ct) =>
        {
            await SpeakInternalAsync(t, isWhisper: false);
        });

        // Use Rx queue for proper serialization
        await Ouroboros.Domain.Autonomous.SpeechQueue.Instance.EnqueueAndWaitAsync(sanitized, _persona.Name);
    }

    /// <summary>
    /// Whispers text using TTS with a softer, more intimate voice style.
    /// Used for inner thoughts and reflections.
    /// </summary>
    public async Task WhisperAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // If voice mode is not initialized, just print text (no TTS)
        if (!_isInitialized)
        {
            AnsiConsole.MarkupLine($"  [rgb(128,0,180)]{Markup.Escape("[💭] " + text)}[/]");
            return;
        }

        // Sanitize for TTS
        string sanitized = SanitizeForTts(text);
        if (string.IsNullOrWhiteSpace(sanitized)) return;

        // Initialize SpeechQueue with whisper TTS
        Ouroboros.Domain.Autonomous.SpeechQueue.Instance.SetSynthesizer(async (t, p, ct) =>
        {
            await SpeakInternalAsync(t, isWhisper: true);
        });

        // Use Rx queue for proper serialization
        await Ouroboros.Domain.Autonomous.SpeechQueue.Instance.EnqueueAndWaitAsync(sanitized, _persona.Name);
    }

    /// <summary>
    /// Internal speech method - does the actual TTS work.
    /// Uses Rx streaming for presence state and voice output events.
    /// </summary>
    private async Task SpeakInternalAsync(string sanitized, bool isWhisper = false)
    {
        _isSpeaking = true;
        _speechDetector?.NotifySelfSpeechStarted();

        // Set presence state to Speaking (or Thinking for whisper/inner thoughts)
        _stream.SetPresenceState(
            isWhisper ? AgentPresenceState.Speaking : AgentPresenceState.Speaking,
            isWhisper ? "Thinking aloud" : "Speaking");

        try
        {
            if (!_config.VoiceOnly)
            {
                if (isWhisper)
                {
                    AnsiConsole.Markup($"  [rgb(128,0,180)]{Markup.Escape("[💭]")}[/] ");
                }
                else
                {
                    AnsiConsole.Markup($"  {OuroborosTheme.Accent("[>]")} {OuroborosTheme.Accent(_persona.Name + ":")} ");
                }
            }

            // Publish the response event to Rx stream
            _stream.PublishResponse(sanitized, isComplete: true, isSentenceEnd: true);

            // Priority: Azure Neural TTS > Local SAPI > Cloud TTS
            // With automatic fallback on rate limiting (429) or other errors
            bool ttsSucceeded = false;

            if (_azureTts != null)
            {
                if (!_config.VoiceOnly) AnsiConsole.MarkupLine(Markup.Escape(sanitized));
                try
                {
                    await _azureTts.SpeakAsync(sanitized, isWhisper);
                    ttsSucceeded = true;
                }
                catch (Exception ex)
                {
                    var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
                    AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} ✗ Azure TTS failed: {Markup.Escape(ex.Message)}, trying fallback...[/]");
                }
            }

            // Fallback to Edge TTS (neural quality, free, no rate limits)
            // Skip if circuit is open (Microsoft blocking the unofficial API)
            if (!ttsSucceeded && _edgeTts != null && !EdgeTtsService.IsCircuitOpen)
            {
                try
                {
                    AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("[>]")} Trying Edge TTS (neural, free)...");
                    TextToSpeechOptions? options = isWhisper ? new TextToSpeechOptions(IsWhisper: true) : null;
                    Result<SpeechResult, string> edgeResult = await _edgeTts.SynthesizeAsync(sanitized, options);
                    if (edgeResult.IsSuccess)
                    {
                        await AudioPlayer.PlayAsync(edgeResult.Value);
                        ttsSucceeded = true;
                    }
                    else
                    {
                        var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
                        AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} ✗ Edge TTS failed: {Markup.Escape(edgeResult.Error)}[/]");
                    }
                }
                catch (Exception ex)
                {
                    var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
                    AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} ✗ Edge TTS failed: {Markup.Escape(ex.Message)}[/]");
                }
            }

            // Fallback to local TTS if Edge also failed (offline fallback)
            if (!ttsSucceeded && _localTts != null)
            {
                try
                {
                    AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("[>]")} Trying Windows SAPI (offline fallback)...");
                    await SpeakWithLocalTtsAsync(sanitized, isWhisper);
                    ttsSucceeded = true;
                }
                catch (Exception ex)
                {
                    var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
                    AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} ✗ Local TTS failed: {Markup.Escape(ex.Message)}[/]");
                }
            }

            // Fallback to cloud TTS (OpenAI)
            if (!ttsSucceeded && _ttsService != null && _ttsService != _azureTts && _ttsService != _localTts)
            {
                try
                {
                    if (_azureTts == null && _localTts == null && !_config.VoiceOnly) AnsiConsole.MarkupLine(Markup.Escape(sanitized));
                    await SpeakWithCloudTtsAsync(sanitized);
                    ttsSucceeded = true;
                }
                catch (Exception ex)
                {
                    var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
                    AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} ✗ Cloud TTS failed: {Markup.Escape(ex.Message)}[/]");
                }
            }

            if (!ttsSucceeded)
            {
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn("[!] All TTS services failed - voice output skipped")}");
            }
        }
        finally
        {
            await Task.Delay(300);
            _isSpeaking = false;
            _speechDetector?.NotifySelfSpeechEnded(cooldownMs: 400);

            // Return to Idle state
            _stream.SetPresenceState(AgentPresenceState.Idle, "Finished speaking");
        }
    }

    private static string SanitizeForTts(string text)
    {
        // Remove code blocks and inline code
        var sanitized = System.Text.RegularExpressions.Regex.Replace(text, @"```[\s\S]*?```", " ");
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"`[^`]+`", " ");

        // Remove emojis - keep only ASCII printable and extended Latin
        var sb = new System.Text.StringBuilder();
        foreach (var c in sanitized)
        {
            if ((c >= 32 && c <= 126) || (c >= 192 && c <= 255))
            {
                sb.Append(c);
            }
            else if (char.IsWhiteSpace(c))
            {
                sb.Append(' ');
            }
        }

        // Normalize whitespace
        return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    private async Task SpeakWithLocalTtsAsync(string text, bool isWhisper = false)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        try
        {
            // For local SAPI, adjust rate and volume for whispering effect
            if (isWhisper && _localTts != null)
            {
                // SAPI doesn't have true whisper, but we can simulate with lower volume/rate
                // This would require modifying LocalWindowsTtsService
            }

            var wordStream = Observable.Create<string>(async (observer, ct) =>
            {
                var chunks = SplitIntoSemanticChunks(text, words);
                foreach (var chunk in chunks)
                {
                    if (ct.IsCancellationRequested) break;
                    if (string.IsNullOrWhiteSpace(chunk)) continue;

                    var chunkWords = chunk.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var speakTask = _localTts!.SpeakAsync(chunk);

                    foreach (var word in chunkWords)
                    {
                        observer.OnNext(word);
                    }

                    await speakTask;
                }
                observer.OnCompleted();
            });

            if (!_config.VoiceOnly)
            {
                if (isWhisper)
                {
                    await wordStream.ForEachAsync(word => AnsiConsole.Markup($"[grey]{Markup.Escape(word + " ")}[/]"));
                }
                else
                {
                    await wordStream.ForEachAsync(word => AnsiConsole.Markup(Markup.Escape(word + " ")));
                }
                AnsiConsole.WriteLine();
            }
            else
            {
                await wordStream.LastOrDefaultAsync();
            }
        }
        catch (Exception ex)
        {
            var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
            AnsiConsole.MarkupLine($"\n  [red]{Markup.Escape(face)} ✗ TTS error: {Markup.Escape(ex.Message)}[/]");
        }
    }

    private async Task SpeakWithCloudTtsAsync(string text)
    {
        try
        {
            var voice = _persona.Voice switch
            {
                "nova" => TtsVoice.Nova,
                "echo" => TtsVoice.Echo,
                "onyx" => TtsVoice.Onyx,
                "fable" => TtsVoice.Fable,
                "shimmer" => TtsVoice.Shimmer,
                _ => TtsVoice.Nova
            };

            var options = new TextToSpeechOptions(Voice: voice, Speed: 1.0f, Format: "mp3");
            var result = await _ttsService!.SynthesizeAsync(text, options);

            await result.Match(
                async speech =>
                {
                    if (!_config.VoiceOnly) AnsiConsole.MarkupLine(Markup.Escape(text));
                    var playResult = await AudioPlayer.PlayAsync(speech);
                    playResult.Match(_ => { }, err =>
                    {
                        var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
                        AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} ✗ Playback: {Markup.Escape(err)}[/]");
                    });
                },
                err =>
                {
                    if (!_config.VoiceOnly) AnsiConsole.MarkupLine(Markup.Escape(text));
                    var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
                    AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} ✗ TTS: {Markup.Escape(err)}[/]");
                    return Task.CompletedTask;
                });
        }
        catch (Exception ex)
        {
            if (!_config.VoiceOnly) AnsiConsole.MarkupLine(Markup.Escape(text));
            var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} ✗ TTS error: {Markup.Escape(ex.Message)}[/]");
        }
    }

    private static IEnumerable<string> SplitIntoSemanticChunks(string text, string[] words)
    {
        var semanticPattern = new System.Text.RegularExpressions.Regex(
            @"(?<=[.!?])\s+|(?<=[;:,\u2014\u2013])\s+|(?<=\band\b|\bor\b|\bbut\b|\bso\b|\bthen\b)\s+",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var chunks = semanticPattern.Split(text)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList();

        if (chunks.Count <= 1 && words.Length > 8)
        {
            chunks.Clear();
            for (int i = 0; i < words.Length; i += 8)
            {
                chunks.Add(string.Join(" ", words.Skip(i).Take(8)));
            }
        }

        return chunks;
    }
}
