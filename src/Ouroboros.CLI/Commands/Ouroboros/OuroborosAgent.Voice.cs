// <copyright file="OuroborosAgent.Voice.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;
using MediatR;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Mediator;
using Spectre.Console;

namespace Ouroboros.CLI.Commands;

public sealed partial class OuroborosAgent
{
    /// <summary>
    /// Strips tool results from text for voice output.
    /// Tool results like "[tool_name]: output" and "[TOOL-RESULT:...]" are removed.
    /// </summary>
    internal static string StripToolResults(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        // Remove lines that match tool result patterns:
        // - [tool_name]: ...
        // - [TOOL-RESULT:tool_name] ...
        // - [propose_intention]: ...
        // - error: ...
        string[] lines = text.Split('\n');
        IEnumerable<string> filtered = lines.Where(line =>
        {
            string trimmed = line.Trim();
            // Skip lines starting with [something]:
            if (Regex.IsMatch(trimmed, @"^\[[\w_:-]+\]:?\s*"))
                return false;
            // Skip lines containing TOOL-RESULT
            if (trimmed.Contains("TOOL-RESULT", StringComparison.OrdinalIgnoreCase))
                return false;
            // Skip error lines
            if (trimmed.StartsWith("error:", StringComparison.OrdinalIgnoreCase))
                return false;
            return true;
        });

        return string.Join("\n", filtered).Trim();
    }

    /// <summary>Uses LLM to integrate tool results naturally into a conversational response.</summary>
    private Task<string> SanitizeToolResultsAsync(string originalResponse, string toolResults)
        => _chatSub.SanitizeToolResultsAsync(originalResponse, toolResults);

    /// <summary>
    /// Speaks text on the voice side channel (fire-and-forget, non-blocking).
    /// Uses the configured persona's voice. Tool results are omitted.
    /// </summary>
    public void Say(string text, string? persona = null)
    {
        if (_voiceSideChannel == null)
        {
            if (_config.Debug) AnsiConsole.MarkupLine(OuroborosTheme.Dim("  [VoiceChannel] Not initialized"));
            return;
        }

        if (!_voiceSideChannel.IsEnabled)
        {
            if (_config.Debug) AnsiConsole.MarkupLine(OuroborosTheme.Dim("  [VoiceChannel] Not enabled (no synthesizer?)"));
            return;
        }

        // Strip tool results from voice output
        var cleanText = StripToolResults(text);
        if (string.IsNullOrWhiteSpace(cleanText)) return;

        if (_config.Debug) AnsiConsole.MarkupLine(OuroborosTheme.Dim($"  [VoiceChannel] Say: {cleanText[..Math.Min(50, cleanText.Length)]}..."));
        _voiceSideChannel.Say(cleanText, persona ?? _config.Persona);
    }

    /// <summary>
    /// Speaks text with a specific persona's voice.
    /// </summary>
    public void SayAs(string persona, string text)
    {
        var cleanText = StripToolResults(text);
        if (!string.IsNullOrWhiteSpace(cleanText))
        {
            _voiceSideChannel?.Say(cleanText, persona);
        }
    }

    /// <summary>
    /// Speaks text and waits for completion (blocking).
    /// Delegates to <see cref="SayAndWaitHandler"/> via MediatR.
    /// </summary>
    public async Task SayAndWaitAsync(string text, string? persona = null, CancellationToken ct = default)
        => await _mediator.Send(new SayAndWaitRequest(text, persona), ct);

    /// <summary>
    /// Announces a system message (high priority).
    /// </summary>
    public void Announce(string text)
    {
        _voiceSideChannel?.Announce(text);
    }

    /// <summary>
    /// Starts listening for voice input using the enhanced listening service.
    /// Supports continuous streaming STT, wake word detection, barge-in, and Whisper fallback.
    /// Delegates to <see cref="StartListeningHandler"/> via MediatR.
    /// </summary>
    public async Task StartListeningAsync()
        => await _mediator.Send(new StartListeningRequest());

    /// <summary>
    /// Stops listening for voice input.
    /// Delegates to <see cref="StopListeningHandler"/> via MediatR.
    /// </summary>
    public void StopListening()
        => _mediator.Send(new StopListeningRequest()).GetAwaiter().GetResult();

    /// <summary>
    /// Continuous listening loop using Azure Speech Recognition with optional Azure TTS response.
    /// </summary>
    private async Task ListenLoopAsync(CancellationToken ct)
    {
        // Get Azure Speech credentials from environment or static configuration
        string? speechKey = Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY")
                       ?? _staticConfiguration?["Azure:Speech:Key"]
                       ?? _config.AzureSpeechKey;
        string speechRegion = Environment.GetEnvironmentVariable("AZURE_SPEECH_REGION")
                          ?? _staticConfiguration?["Azure:Speech:Region"]
                          ?? _config.AzureSpeechRegion
                          ?? "eastus";

        if (string.IsNullOrEmpty(speechKey))
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn(GetLocalizedString("voice_requires_key")));
            return;
        }

        Microsoft.CognitiveServices.Speech.SpeechConfig config = Microsoft.CognitiveServices.Speech.SpeechConfig.FromSubscription(speechKey, speechRegion);

        // Set speech recognition language based on culture if available
        config.SpeechRecognitionLanguage = _config.Culture ?? "en-US";

        using Microsoft.CognitiveServices.Speech.SpeechRecognizer recognizer = new Microsoft.CognitiveServices.Speech.SpeechRecognizer(config);

        while (!ct.IsCancellationRequested)
        {
            AnsiConsole.Markup(OuroborosTheme.Ok("  🎤 "));

            Microsoft.CognitiveServices.Speech.SpeechRecognitionResult result = await recognizer.RecognizeOnceAsync();

            if (result.Reason == Microsoft.CognitiveServices.Speech.ResultReason.RecognizedSpeech)
            {
                string text = result.Text.Trim();
                if (string.IsNullOrEmpty(text)) continue;

                // Check for stop commands
                if (text.ToLowerInvariant().Contains("stop listening") ||
                    text.ToLowerInvariant().Contains("disable voice"))
                {
                    StopListening();
                    _autonomousCoordinator?.ProcessCommand("/listen off");
                    break;
                }

                AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  {GetLocalizedString("you_said")} {text}"));

                // Process as regular input
                string response = await ChatAsync(text);

                // Display response
                AnsiConsole.MarkupLine($"\n  [rgb(148,103,189)]{Markup.Escape(response)}[/]");

                // Speak response using Azure TTS if enabled
                if (_config.AzureTts && !string.IsNullOrEmpty(speechKey))
                {
                    try
                    {
                        await SpeakResponseWithAzureTtsAsync(response, speechKey, speechRegion, ct);
                    }
                    catch (Exception ex)
                    {
                        if (_config.Debug)
                        {
                            AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  ⚠ Azure TTS error: {ex.Message}"));
                        }
                    }
                }
            }
            else if (result.Reason == Microsoft.CognitiveServices.Speech.ResultReason.NoMatch)
            {
                // No speech detected, continue listening
            }
            else if (result.Reason == Microsoft.CognitiveServices.Speech.ResultReason.Canceled)
            {
                var cancellation = Microsoft.CognitiveServices.Speech.CancellationDetails.FromResult(result);
                if (cancellation.Reason == Microsoft.CognitiveServices.Speech.CancellationReason.Error)
                {
                    var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
                    AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} ✗ {Markup.Escape($"Speech recognition error: {cancellation.ErrorDetails}")}[/]");
                }
                break;
            }
        }
    }

    /// <summary>
    /// Speaks a response using Azure TTS with configured voice.
    /// Supports barge-in via the CancellationToken — cancelling stops synthesis immediately.
    /// </summary>
    private async Task SpeakResponseWithAzureTtsAsync(string text, string key, string region, CancellationToken ct)
    {
        try
        {
            var config = Microsoft.CognitiveServices.Speech.SpeechConfig.FromSubscription(key, region);

            // Auto-select voice based on culture (unless user explicitly set a non-default voice)
            var voiceName = GetEffectiveVoice();
            config.SpeechSynthesisVoiceName = voiceName;

            // Use default speaker
            using var speechSynthesizer = new Microsoft.CognitiveServices.Speech.SpeechSynthesizer(config);

            // Register cancellation to stop synthesis (barge-in support)
            ct.Register(() =>
            {
                try { _ = speechSynthesizer.StopSpeakingAsync(); }
                catch { /* Best effort */ }
            });

            // Detect the response language via LanguageSubsystem (Ollama LLM → heuristic fallback).
            // For cross-lingual voices <speak> carries the voice's primary locale,
            // <voice xml:lang> carries the target language.
            var voicePrimaryLocale = voiceName.Length >= 5 ? voiceName[..5] : "en-US";
            var responseLang = await Subsystems.LanguageSubsystem
                .DetectStaticAsync(text, ct).ConfigureAwait(false);
            var voiceLang = responseLang.Culture;
            var ssml = $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='{voicePrimaryLocale}'>
    <voice name='{voiceName}' xml:lang='{voiceLang}'>
        {System.Net.WebUtility.HtmlEncode(text)}
    </voice>
</speak>";

            var result = await speechSynthesizer.SpeakSsmlAsync(ssml);

            if (result.Reason != Microsoft.CognitiveServices.Speech.ResultReason.SynthesizingAudioCompleted)
            {
                if (_config.Debug)
                {
                    _output.WriteDebug($"[Azure TTS] Synthesis issue: {result.Reason}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during barge-in
        }
        catch (Exception ex)
        {
            if (_config.Debug)
            {
                _output.WriteDebug($"[Azure TTS] Error: {ex.Message}");
            }
        }
    }

    private static async Task SpeakWithSapiAsync(string text, PersonaVoice voice, CancellationToken ct)
    {
        // Try Azure TTS first (higher quality, Cortana-like voices)
        // Check user secrets first, then environment variables
        var azureKey = _staticConfiguration?["Azure:Speech:Key"]
            ?? Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
        var azureRegion = _staticConfiguration?["Azure:Speech:Region"]
            ?? Environment.GetEnvironmentVariable("AZURE_SPEECH_REGION");

        if (!string.IsNullOrEmpty(azureKey) && !string.IsNullOrEmpty(azureRegion))
        {
            if (await SpeakWithAzureTtsAsync(text, voice, azureKey, azureRegion, ct))
                return;
        }

        // Fallback to Windows SAPI
        await SpeakWithWindowsSapiAsync(text, voice, ct);
    }


    private static async Task<bool> SpeakWithAzureTtsAsync(string text, PersonaVoice voice, string key, string region, CancellationToken ct)
    {
        try
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Dim($"  [Azure TTS] Speaking as {voice.PersonaName}: {text[..Math.Min(40, text.Length)]}..."));

            var config = Microsoft.CognitiveServices.Speech.SpeechConfig.FromSubscription(key, region);

            // Detect the response language via LanguageSubsystem (Ollama LLM → heuristic fallback).
            var detectedLang = await Ouroboros.CLI.Subsystems.LanguageSubsystem
                .DetectStaticAsync(text, ct).ConfigureAwait(false);
            var culture = detectedLang.Culture;
            var isGerman = culture.StartsWith("de", StringComparison.OrdinalIgnoreCase);

            // Select Azure Neural voice based on culture and persona
            string azureVoice;
            if (isGerman)
            {
                // German voices for all personas
                azureVoice = voice.PersonaName.ToUpperInvariant() switch
                {
                    // Iaret: multilingual voice, speaks German via cross-lingual SSML — no locale switch needed.
                    "IARET" => "en-US-JennyMultilingualNeural",
                    "OUROBOROS" => "de-DE-KatjaNeural",   // German female (Cortana-like)
                    "ARIA" => "de-DE-AmalaNeural",        // German expressive female
                    "ECHO" => "de-AT-IngridNeural",       // Austrian German female
                    "SAGE" => "de-DE-KatjaNeural",        // German calm female
                    "ATLAS" => "de-DE-ConradNeural",      // German male
                    "SYSTEM" => "de-DE-KatjaNeural",      // System messages
                    "USER" => "de-DE-ConradNeural",       // User persona - male
                    "USER_PERSONA" => "de-DE-ConradNeural",
                    _ => "de-DE-KatjaNeural"
                };
            }
            else
            {
                // English/other voices (default)
                azureVoice = voice.PersonaName.ToUpperInvariant() switch
                {
                    // Iaret: always Cortana/Jenny multilingual voice.
                    "IARET" => "en-US-JennyMultilingualNeural",
                    "OUROBOROS" => "en-US-JennyNeural",    // Cortana-like voice!
                    "ARIA" => "en-US-AriaNeural",          // Expressive female
                    "ECHO" => "en-GB-SoniaNeural",         // UK female
                    "SAGE" => "en-US-SaraNeural",          // Calm female
                    "ATLAS" => "en-US-GuyNeural",          // Male
                    "SYSTEM" => "en-US-JennyNeural",       // System messages
                    "USER" => "en-US-GuyNeural",           // User persona - male (distinct from Jenny)
                    "USER_PERSONA" => "en-US-GuyNeural",
                    _ => "en-US-JennyNeural"
                };
            }

            config.SpeechSynthesisVoiceName = azureVoice;

            // Use mythic SSML styling for Cortana-like voices (Jenny or Katja)
            var useFriendlyStyle = azureVoice.Contains("Jenny") || azureVoice.Contains("Katja");
            var azureVoicePrimaryLocale = azureVoice.Length >= 5 ? azureVoice[..5] : culture;
            var ssml = useFriendlyStyle
                ? $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis'
                    xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='{azureVoicePrimaryLocale}'>
                    <voice name='{azureVoice}' xml:lang='{culture}'>
                        <mstts:express-as style='friendly' styledegree='0.8'>
                            <prosody rate='-5%' pitch='+8%' volume='+3%'>
                                <mstts:audioduration value='1.1'/>
                                {System.Security.SecurityElement.Escape(text)}
                            </prosody>
                        </mstts:express-as>
                        <mstts:audioeffect type='eq_car'/>
                    </voice>
                </speak>"
                : $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='{azureVoicePrimaryLocale}'>
                    <voice name='{azureVoice}' xml:lang='{culture}'>
                        <prosody rate='0%'>{System.Security.SecurityElement.Escape(text)}</prosody>
                    </voice>
                </speak>";

            using var synthesizer = new Microsoft.CognitiveServices.Speech.SpeechSynthesizer(config);

            var result = await synthesizer.SpeakSsmlAsync(ssml);

            if (result.Reason == Microsoft.CognitiveServices.Speech.ResultReason.SynthesizingAudioCompleted)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Dim("  [Azure TTS] Done"));
                return true;
            }

            if (result.Reason == Microsoft.CognitiveServices.Speech.ResultReason.Canceled)
            {
                var cancellation = Microsoft.CognitiveServices.Speech.SpeechSynthesisCancellationDetails.FromResult(result);
                AnsiConsole.MarkupLine($"[red]{Markup.Escape($"  [Azure TTS Error] {cancellation.ErrorDetails}")}[/]");
            }

            return false;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape($"  [Azure TTS Exception] {ex.Message}")}[/]");
            return false; // Fall back to SAPI
        }
    }


    private static async Task SpeakWithWindowsSapiAsync(string text, PersonaVoice voice, CancellationToken ct)
    {
        try
        {
            // Use Windows Speech via PowerShell with persona-specific rate/pitch
            var escapedText = text
                .Replace("'", "''")
                .Replace("\r", " ")
                .Replace("\n", " ");

            // Convert persona rate (0.5-1.5) to SAPI rate (-5 to +5)
            var rate = (int)((voice.Rate - 1.0f) * 10);

            // Select voice based on persona - use different voices for variety
            // Available voices depend on system - check with GetInstalledVoices()
            // Common: Microsoft David (male), Microsoft Zira (female), Microsoft Hedda (German female)
            var voiceSelector = voice.PersonaName.ToUpperInvariant() switch
            {
                "OUROBOROS" => "'Zira'",     // Default: Zira (US female) - closest to Cortana available
                "ARIA" => "'Zira'",          // Female voice
                "ECHO" => "'Hazel'",         // UK female
                "SAGE" => "'Hedda'",         // German female
                "ATLAS" => "'David'",        // David with rate adjustment
                "SYSTEM" => "'Zira'",        // System announcements
                "USER" => "'David'",         // User persona - David (US male, distinct from Zira)
                "USER_PERSONA" => "'David'", // User persona alternate key
                _ => "'Zira'"                // Default fallback
            };

            var script = $@"
Add-Type -AssemblyName System.Speech
$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
$voices = $synth.GetInstalledVoices() | Where-Object {{ $_.VoiceInfo.Culture.Name -like 'en-*' }}
$targetNames = @({voiceSelector})
$selectedVoice = $null
foreach ($target in $targetNames) {{
    $match = $voices | Where-Object {{ $_.VoiceInfo.Name -like ""*$target*"" }} | Select-Object -First 1
    if ($match) {{ $selectedVoice = $match; break }}
}}
if ($selectedVoice) {{ $synth.SelectVoice($selectedVoice.VoiceInfo.Name) }}
elseif ($voices.Count -gt 0) {{ $synth.SelectVoice($voices[0].VoiceInfo.Name) }}
$synth.Rate = {Math.Clamp(rate, -10, 10)}
$synth.Volume = {voice.Volume}
$synth.Speak('{escapedText}')
$synth.Dispose()
";
            using var process = new System.Diagnostics.Process();
            process.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"{script.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            process.Start();

            // Track the process so we can kill it on exit
            VoiceSubsystem.TrackSpeechProcess(process);

            try
            {
                await process.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                // Kill the process if cancelled
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                throw;
            }
            finally
            {
                // Remove from tracking (best effort - ConcurrentBag doesn't have Remove)
            }
        }
        catch
        {
            // Silently fail if SAPI not available
        }
    }

}
