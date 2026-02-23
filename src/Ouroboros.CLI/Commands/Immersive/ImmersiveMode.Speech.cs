// <copyright file="ImmersiveMode.Speech.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.CLI.Commands;

using Ouroboros.Options;
using Ouroboros.Providers.SpeechToText;
using Ouroboros.Providers.TextToSpeech;
using Ouroboros.Speech;

public sealed partial class ImmersiveMode
{
    private async Task<(ITextToSpeechService?, ISpeechToTextService?, AdaptiveSpeechDetector?)> InitializeSpeechServicesAsync(IVoiceOptions? options = null)
    {
        ITextToSpeechService? tts = null;
        ISpeechToTextService? stt = null;
        AdaptiveSpeechDetector? detector = null;

        // Azure Neural TTS takes first priority when configured (matches OuroborosAgent default)
        if (options is ImmersiveCommandVoiceOptions ico && ico.AzureTts)
        {
            var azureKey = ico.AzureSpeechKey
                ?? Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
            if (!string.IsNullOrEmpty(azureKey))
            {
                try
                {
                    tts = new AzureNeuralTtsService(azureKey, ico.AzureSpeechRegion, ico.Persona ?? "Iaret");
                    Console.WriteLine($"  [OK] Voice output: Azure Neural TTS ({ico.TtsVoice})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  [!] Azure TTS unavailable: {ex.Message}");
                }
            }
        }

        string? openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        // Initialize TTS (fallbacks when Azure not configured or unavailable)
        if (tts == null && LocalWindowsTtsService.IsAvailable())
        {
            try
            {
                tts = new LocalWindowsTtsService(rate: 1, volume: 100, useEnhancedProsody: true);
                Console.WriteLine("  [OK] Voice output: Windows SAPI");
            }
            catch { }
        }

        if (tts == null && !string.IsNullOrEmpty(openAiKey))
        {
            try
            {
                tts = new OpenAiTextToSpeechService(openAiKey);
                Console.WriteLine("  [OK] Voice output: OpenAI TTS");
            }
            catch { }
        }

        // Initialize STT
        if (!string.IsNullOrEmpty(openAiKey))
        {
            try
            {
                var whisperNet = WhisperNetService.FromModelSize("base");
                if (await whisperNet.IsAvailableAsync())
                {
                    stt = whisperNet;
                    Console.WriteLine("  [OK] Voice input: Whisper.net");

                    detector = new AdaptiveSpeechDetector(new AdaptiveSpeechDetector.SpeechDetectionConfig(
                        InitialThreshold: 0.03,
                        SpeechOnsetFrames: 2,
                        SpeechOffsetFrames: 6,
                        AdaptationRate: 0.015,
                        SpeechToNoiseRatio: 2.0
                    ));
                }
            }
            catch { }
        }

        if (tts == null) Console.WriteLine("  [~] Voice output: Text only (set OPENAI_API_KEY for voice)");
        if (stt == null) Console.WriteLine("  [~] Voice input: Keyboard only (install Whisper for voice)");

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
