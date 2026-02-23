// <copyright file="ImmersiveMode.Speech.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.CLI.Commands;

using Ouroboros.CLI.Infrastructure;
using Ouroboros.Options;
using Ouroboros.Providers.SpeechToText;
using Ouroboros.Providers.TextToSpeech;
using Ouroboros.Speech;
using Spectre.Console;

public sealed partial class ImmersiveMode
{
    private async Task<(ITextToSpeechService?, ISpeechToTextService?, AdaptiveSpeechDetector?)> InitializeSpeechServicesAsync(IVoiceOptions? options = null)
    {
        // Extract Azure config from options
        string? azureKey = null;
        string azureRegion = "eastus";
        string personaName = "Iaret";
        string ttsVoice = "en-US-JennyMultilingualNeural";
        bool useAzure = false;

        if (options is ImmersiveCommandVoiceOptions ico)
        {
            useAzure = ico.AzureTts;
            azureKey = ico.AzureSpeechKey ?? Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
            azureRegion = ico.AzureSpeechRegion;
            personaName = ico.Persona ?? "Iaret";
            ttsVoice = ico.TtsVoice;
        }

        // TTS via SharedAgentBootstrap (Azure → Local → OpenAI fallback chain)
        var tts = useAzure
            ? Services.SharedAgentBootstrap.CreateTtsService(
                azureKey, azureRegion, personaName, ttsVoice,
                preferLocal: true,
                log: msg => AnsiConsole.MarkupLine(OuroborosTheme.Ok($"  [OK] {Markup.Escape(msg)}")))
            : Services.SharedAgentBootstrap.CreateTtsService(
                null, azureRegion, personaName, ttsVoice,
                preferLocal: true,
                log: msg => AnsiConsole.MarkupLine(OuroborosTheme.Ok($"  [OK] {Markup.Escape(msg)}")));

        // STT via SharedAgentBootstrap
        var stt = await Services.SharedAgentBootstrap.CreateSttService(
            log: msg => AnsiConsole.MarkupLine(OuroborosTheme.Ok($"  [OK] {Markup.Escape(msg)}")));

        // Create speech detector if STT is available
        AdaptiveSpeechDetector? detector = null;
        if (stt != null)
        {
            detector = Services.SharedAgentBootstrap.CreateSpeechDetector();
        }

        if (tts == null) AnsiConsole.MarkupLine(OuroborosTheme.Dim("  [~] Voice output: Text only (set OPENAI_API_KEY for voice)"));
        if (stt == null) AnsiConsole.MarkupLine(OuroborosTheme.Dim("  [~] Voice input: Keyboard only (install Whisper for voice)"));

        return (tts, stt, detector);
    }

    /// <summary>
    /// Reads a line of input while tracking the buffer so proactive messages can restore it.
    /// </summary>
    private async Task<string?> ReadLinePreservingBufferAsync(CancellationToken ct = default)
    {
        lock (_inputLock)
        {
            _currentInputBuffer.Clear();
        }

        while (!ct.IsCancellationRequested)
        {
            if (!Console.KeyAvailable)
            {
                await Task.Delay(10, ct);
                continue;
            }

            var keyInfo = Console.ReadKey(intercept: true);

            if (keyInfo.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                string result;
                lock (_inputLock)
                {
                    result = _currentInputBuffer.ToString();
                    _currentInputBuffer.Clear();
                }
                return result;
            }
            else if (keyInfo.Key == ConsoleKey.Backspace)
            {
                lock (_inputLock)
                {
                    if (_currentInputBuffer.Length > 0)
                    {
                        _currentInputBuffer.Remove(_currentInputBuffer.Length - 1, 1);
                        // Erase character on console
                        Console.Write("\b \b");
                    }
                }
            }
            else if (keyInfo.Key == ConsoleKey.Escape)
            {
                // Clear the line
                lock (_inputLock)
                {
                    var len = _currentInputBuffer.Length;
                    _currentInputBuffer.Clear();
                    Console.Write(new string('\b', len) + new string(' ', len) + new string('\b', len));
                }
            }
            else if (!char.IsControl(keyInfo.KeyChar))
            {
                lock (_inputLock)
                {
                    _currentInputBuffer.Append(keyInfo.KeyChar);
                }
                Console.Write(keyInfo.KeyChar);
            }
        }

        return null;
    }
}
