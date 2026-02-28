// <copyright file="TextToSpeechCliSteps.Parsing.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Providers.TextToSpeech;

namespace Ouroboros.Application;

/// <summary>
/// Partial class containing the AskAndSay/TtsStatus pipeline steps
/// and all helper/argument-parsing logic for text-to-speech CLI steps.
/// </summary>
public static partial class TextToSpeechCliSteps
{
    /// <summary>
    /// Ask a question and speak the answer directly (no file).
    /// Usage: AskAndSay('What is the weather?')
    /// Usage: AskAndSay('Tell me a joke;voice=fable')
    /// </summary>
    [PipelineToken("AskAndSay", "SayAnswer", "VoiceAnswer")]
    public static Step<CliPipelineState, CliPipelineState> AskAndSay(string? args = null)
        => async s =>
        {
            SpeakConfig config = ParseSpeakArgs(args);

            if (string.IsNullOrEmpty(config.Text))
            {
                Console.WriteLine("[tts] Error: Question required. Usage: AskAndSay('your question')");
                return s;
            }

            // Initialize TTS if needed
            if (currentService == null)
            {
                string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                if (!string.IsNullOrEmpty(apiKey))
                {
                    currentService = new OpenAiTextToSpeechService(apiKey);
                }
                else
                {
                    Console.WriteLine("[tts] Error: Not initialized.");
                    return s;
                }
            }

            // Ask the LLM
            Console.WriteLine($"[tts] Question: {config.Text}");

            string response;
            try
            {
                response = await s.Llm.InnerModel.GenerateTextAsync(config.Text);
                Console.WriteLine($"[LLM] {TruncateText(response, 200)}");
            }
            catch (OperationCanceledException) { throw; }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[tts] LLM error: {ex.Message}");
                return s;
            }

            // Speak the response directly
            TtsVoice voice = ParseVoice(config.Voice);
            TextToSpeechOptions options = new TextToSpeechOptions(
                Voice: voice,
                Speed: config.Speed,
                Format: "mp3");

            Result<SpeechResult, string> synthesisResult = await currentService.SynthesizeAsync(response, options);

            await synthesisResult.Match(
                async speech =>
                {
                    Console.WriteLine("[tts] \ud83d\udd0a Playing response...");
                    Result<bool, string> playResult = await AudioPlayer.PlayAsync(speech);
                    playResult.Match(
                        _ =>
                        {
                            Console.WriteLine("[tts] \u2713 Playback complete");
                            s.Output = response;
                        },
                        error => Console.WriteLine($"[tts] Playback error: {error}"));
                },
                error =>
                {
                    Console.WriteLine($"[tts] Error: {error}");
                    return Task.CompletedTask;
                });

            return s;
        };

    /// <summary>
    /// Check TTS service status.
    /// Usage: TtsStatus()
    /// </summary>
    [PipelineToken("TtsStatus")]
    public static Step<CliPipelineState, CliPipelineState> TtsStatus(string? args = null)
        => async s =>
        {
            if (currentService == null)
            {
                Console.WriteLine("[tts] Not initialized. Use TtsInit() to configure.");
                return s;
            }

            Console.WriteLine($"[tts] Provider: {currentService.ProviderName}");
            Console.WriteLine($"[tts] Voices: {string.Join(", ", currentService.AvailableVoices)}");
            Console.WriteLine($"[tts] Formats: {string.Join(", ", currentService.SupportedFormats)}");
            Console.WriteLine($"[tts] Max length: {currentService.MaxInputLength} characters");

            bool available = await currentService.IsAvailableAsync();
            Console.WriteLine($"[tts] Available: {(available ? "Yes" : "No")}");

            return s;
        };

    #region Helpers

    private static TtsVoice ParseVoice(string? voiceName)
    {
        if (string.IsNullOrEmpty(voiceName))
        {
            return TtsVoice.Alloy;
        }

        return voiceName.ToLowerInvariant() switch
        {
            "alloy" => TtsVoice.Alloy,
            "echo" => TtsVoice.Echo,
            "fable" => TtsVoice.Fable,
            "onyx" => TtsVoice.Onyx,
            "nova" => TtsVoice.Nova,
            "shimmer" => TtsVoice.Shimmer,
            _ => TtsVoice.Alloy,
        };
    }

    private static string GetFormatFromPath(string path)
    {
        string ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "mp3" or "opus" or "aac" or "flac" or "wav" or "pcm" => ext,
            _ => "mp3",
        };
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        return text[..maxLength] + "...";
    }

    #endregion

    #region Argument Parsing

    private sealed class TtsConfig
    {
        public string? ApiKey { get; set; }

        public string? Endpoint { get; set; }

        public string? Model { get; set; }
    }

    private sealed class SpeakConfig
    {
        public string? Text { get; set; }

        public string? OutputPath { get; set; }

        public string? Voice { get; set; }

        public double Speed { get; set; } = 1.0;
    }

    private static TtsConfig ParseTtsConfig(string? args)
    {
        TtsConfig config = new TtsConfig();
        if (string.IsNullOrWhiteSpace(args))
        {
            return config;
        }

        args = args.Trim().Trim('\'', '"');

        foreach (string part in args.Split(';'))
        {
            string trimmed = part.Trim();
            int eqIndex = trimmed.IndexOf('=');

            if (eqIndex > 0)
            {
                string key = trimmed[..eqIndex].Trim().ToLowerInvariant();
                string value = trimmed[(eqIndex + 1)..].Trim();

                switch (key)
                {
                    case "apikey" or "api_key" or "key":
                        config.ApiKey = value;
                        break;
                    case "endpoint" or "url":
                        config.Endpoint = value;
                        break;
                    case "model":
                        config.Model = value;
                        break;
                }
            }
        }

        return config;
    }

    private static SpeakConfig ParseSpeakArgs(string? args)
    {
        SpeakConfig config = new SpeakConfig();
        if (string.IsNullOrWhiteSpace(args))
        {
            return config;
        }

        args = args.Trim().Trim('\'', '"');
        List<string> textParts = new List<string>();

        foreach (string part in args.Split(';'))
        {
            string trimmed = part.Trim();
            int eqIndex = trimmed.IndexOf('=');

            if (eqIndex > 0)
            {
                string key = trimmed[..eqIndex].Trim().ToLowerInvariant();
                string value = trimmed[(eqIndex + 1)..].Trim();

                switch (key)
                {
                    case "output" or "out" or "path" or "file":
                        config.OutputPath = value;
                        break;
                    case "voice":
                        config.Voice = value;
                        break;
                    case "speed":
                        if (double.TryParse(value, out double speed))
                        {
                            config.Speed = speed;
                        }

                        break;
                    case "text":
                        config.Text = value;
                        break;
                }
            }
            else if (!string.IsNullOrEmpty(trimmed))
            {
                // Accumulate non-key=value parts as text
                textParts.Add(trimmed);
            }
        }

        // Join text parts if text wasn't explicitly set
        if (string.IsNullOrEmpty(config.Text) && textParts.Count > 0)
        {
            config.Text = string.Join("; ", textParts);
        }

        return config;
    }

    #endregion
}
