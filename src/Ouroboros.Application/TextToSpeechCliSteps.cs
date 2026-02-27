// <copyright file="TextToSpeechCliSteps.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Providers.TextToSpeech;

namespace Ouroboros.Application;

/// <summary>
/// CLI Pipeline steps for text-to-speech operations.
/// Supports OpenAI TTS API and other text-to-speech providers.
/// Note: Use semicolon (;) as separator inside quotes since pipe (|) is the DSL step separator.
/// </summary>
public static partial class TextToSpeechCliSteps
{
    /// <summary>
    /// Current text-to-speech service instance.
    /// </summary>
    private static ITextToSpeechService? currentService;

    /// <summary>
    /// Initialize TTS service.
    /// Usage: TtsInit('openai;apiKey=sk-xxx;model=tts-1')
    /// Usage: TtsInit() - uses OpenAI with OPENAI_API_KEY env var
    /// Usage: TtsInit('openai;model=tts-1-hd') - use HD model
    /// </summary>
    [PipelineToken("TtsInit", "InitTts", "SpeakInit")]
    public static Step<CliPipelineState, CliPipelineState> TtsInit(string? args = null)
        => s =>
        {
            TtsConfig config = ParseTtsConfig(args);

            try
            {
                string apiKey = config.ApiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                    ?? throw new InvalidOperationException("API key required. Set OPENAI_API_KEY or pass apiKey=...");

                currentService = new OpenAiTextToSpeechService(
                    apiKey,
                    config.Endpoint,
                    config.Model ?? "tts-1");

                if (s.Trace)
                {
                    Console.WriteLine($"[tts] Initialized {currentService.ProviderName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[tts] Failed to initialize: {ex.Message}");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Convert text to speech and save to file.
    /// Usage: Speak('Hello world;output=greeting.mp3')
    /// Usage: Speak('Hello world;output=greeting.mp3;voice=nova;speed=1.2')
    /// The text can also come from s.Output if no text specified.
    /// </summary>
    [PipelineToken("Speak", "TtsSpeak", "TextToSpeech")]
    public static Step<CliPipelineState, CliPipelineState> Speak(string? args = null)
        => async s =>
        {
            SpeakConfig config = ParseSpeakArgs(args);

            // Get text from args or from pipeline output
            string? text = config.Text ?? s.Output;
            if (string.IsNullOrWhiteSpace(text))
            {
                Console.WriteLine("[tts] Error: No text to speak. Provide text or use after a step that sets Output.");
                return s;
            }

            if (string.IsNullOrEmpty(config.OutputPath))
            {
                Console.WriteLine("[tts] Error: Output path required. Usage: Speak('text;output=file.mp3')");
                return s;
            }

            if (currentService == null)
            {
                // Auto-initialize with OpenAI if API key is available
                string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                if (!string.IsNullOrEmpty(apiKey))
                {
                    currentService = new OpenAiTextToSpeechService(apiKey);
                    if (s.Trace)
                    {
                        Console.WriteLine("[tts] Auto-initialized OpenAI TTS");
                    }
                }
                else
                {
                    Console.WriteLine("[tts] Error: Not initialized. Use TtsInit() first or set OPENAI_API_KEY");
                    return s;
                }
            }

            Console.WriteLine($"[tts] Synthesizing: {TruncateText(text, 50)}");
            Console.WriteLine($"[tts] Output: {config.OutputPath}");
            Console.WriteLine($"[tts] Voice: {config.Voice}, Speed: {config.Speed:F1}x");

            TtsVoice voice = ParseVoice(config.Voice);
            TextToSpeechOptions options = new TextToSpeechOptions(
                Voice: voice,
                Speed: config.Speed,
                Format: GetFormatFromPath(config.OutputPath));

            Result<string, string> result = await currentService.SynthesizeToFileAsync(
                text,
                config.OutputPath,
                options);

            result.Match(
                path =>
                {
                    Console.WriteLine($"[tts] \u2713 Audio saved to: {path}");
                    s.Output = path;
                },
                error =>
                {
                    Console.WriteLine($"[tts] Error: {error}");
                });

            return s;
        };

    /// <summary>
    /// Speak the LLM response - combines Ask with text-to-speech.
    /// Usage: SpeakAnswer('What is the weather?;output=answer.mp3')
    /// </summary>
    [PipelineToken("SpeakAnswer", "TtsAnswer")]
    public static Step<CliPipelineState, CliPipelineState> SpeakAnswer(string? args = null)
        => async s =>
        {
            SpeakConfig config = ParseSpeakArgs(args);

            if (string.IsNullOrEmpty(config.Text))
            {
                Console.WriteLine("[tts] Error: Question required. Usage: SpeakAnswer('question;output=answer.mp3')");
                return s;
            }

            if (string.IsNullOrEmpty(config.OutputPath))
            {
                Console.WriteLine("[tts] Error: Output path required.");
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
            catch (Exception ex)
            {
                Console.WriteLine($"[tts] LLM error: {ex.Message}");
                return s;
            }

            // Speak the response
            TtsVoice voice = ParseVoice(config.Voice);
            TextToSpeechOptions options = new TextToSpeechOptions(
                Voice: voice,
                Speed: config.Speed,
                Format: GetFormatFromPath(config.OutputPath));

            Result<string, string> result = await currentService.SynthesizeToFileAsync(
                response,
                config.OutputPath,
                options);

            result.Match(
                path =>
                {
                    Console.WriteLine($"[tts] \u2713 Audio saved to: {path}");
                    s.Output = response;
                    s.Context = path;
                },
                error => Console.WriteLine($"[tts] Error: {error}"));

            return s;
        };

    /// <summary>
    /// List available voices.
    /// Usage: TtsVoices()
    /// </summary>
    [PipelineToken("TtsVoices", "ListVoices")]
    public static Step<CliPipelineState, CliPipelineState> TtsVoices(string? args = null)
        => s =>
        {
            Console.WriteLine("[tts] Available voices:");
            Console.WriteLine("  \u2022 alloy   - Neutral, balanced voice");
            Console.WriteLine("  \u2022 echo    - Warm, conversational voice");
            Console.WriteLine("  \u2022 fable   - Expressive, narrative voice");
            Console.WriteLine("  \u2022 onyx    - Deep, authoritative voice");
            Console.WriteLine("  \u2022 nova    - Friendly, upbeat voice");
            Console.WriteLine("  \u2022 shimmer - Soft, gentle voice");

            return Task.FromResult(s);
        };

    /// <summary>
    /// Speak text directly through speakers (no file output).
    /// Usage: Say('Hello world')
    /// Usage: Say('Hello world;voice=nova;speed=1.2')
    /// Usage: Say() - speaks the current pipeline Output
    /// </summary>
    [PipelineToken("Say", "SayIt", "SayText", "PlaySpeech")]
    public static Step<CliPipelineState, CliPipelineState> Say(string? args = null)
        => async s =>
        {
            SpeakConfig config = ParseSpeakArgs(args);

            // Get text from args or from pipeline output
            string? text = config.Text ?? s.Output;
            if (string.IsNullOrWhiteSpace(text))
            {
                Console.WriteLine("[tts] Error: No text to speak. Provide text or use after a step that sets Output.");
                return s;
            }

            if (currentService == null)
            {
                // Auto-initialize with OpenAI if API key is available
                string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                if (!string.IsNullOrEmpty(apiKey))
                {
                    currentService = new OpenAiTextToSpeechService(apiKey);
                    if (s.Trace)
                    {
                        Console.WriteLine("[tts] Auto-initialized OpenAI TTS");
                    }
                }
                else
                {
                    Console.WriteLine("[tts] Error: Not initialized. Use TtsInit() first or set OPENAI_API_KEY");
                    return s;
                }
            }

            Console.WriteLine($"[tts] Speaking: {TruncateText(text, 50)}");
            Console.WriteLine($"[tts] Voice: {config.Voice ?? "alloy"}, Speed: {config.Speed:F1}x");

            TtsVoice voice = ParseVoice(config.Voice);
            TextToSpeechOptions options = new TextToSpeechOptions(
                Voice: voice,
                Speed: config.Speed,
                Format: "mp3");

            Result<SpeechResult, string> synthesisResult = await currentService.SynthesizeAsync(text, options);

            await synthesisResult.Match(
                async speech =>
                {
                    Console.WriteLine("[tts] \ud83d\udd0a Playing audio...");
                    Result<bool, string> playResult = await AudioPlayer.PlayAsync(speech);
                    playResult.Match(
                        _ => Console.WriteLine("[tts] \u2713 Playback complete"),
                        error => Console.WriteLine($"[tts] Playback error: {error}"));
                },
                error =>
                {
                    Console.WriteLine($"[tts] Error: {error}");
                    return Task.CompletedTask;
                });

            return s;
        };
}
