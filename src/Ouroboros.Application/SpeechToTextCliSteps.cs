// <copyright file="SpeechToTextCliSteps.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Providers.SpeechToText;

namespace Ouroboros.Application;

/// <summary>
/// CLI Pipeline steps for speech-to-text operations.
/// Supports OpenAI Whisper API and local Whisper installations.
/// Note: Use semicolon (;) as separator inside quotes since pipe (|) is the DSL step separator.
/// </summary>
/// <remarks>
/// This class is split into partial files:
/// - SpeechToTextCliSteps.cs (this file): Core transcription pipeline steps
/// - SpeechToTextCliSteps.Recording.cs: Microphone recording and voice interaction steps
/// - SpeechToTextCliSteps.Parsing.cs: Argument parsing helpers and configuration types
/// </remarks>
public static partial class SpeechToTextCliSteps
{
    /// <summary>
    /// Current speech-to-text service instance.
    /// </summary>
    private static ISpeechToTextService? currentService;

    /// <summary>
    /// Initialize Whisper for speech-to-text (offline-first).
    /// Usage: SttInit() - auto-detects local Whisper, falls back to OpenAI
    /// Usage: SttInit('local;model=small') - force local Whisper (default)
    /// Usage: SttInit('openai;apiKey=sk-xxx') - force OpenAI API
    /// </summary>
    [PipelineToken("SttInit", "WhisperInit", "InitStt")]
    public static Step<CliPipelineState, CliPipelineState> SttInit(string? args = null)
        => async s =>
        {
            SttConfig config = ParseSttConfig(args);

            try
            {
                // Offline-first: try local Whisper unless explicitly requesting OpenAI
                if (config.Provider.ToLowerInvariant() == "openai")
                {
                    // Explicitly requested OpenAI
                    string apiKey = config.ApiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                        ?? throw new InvalidOperationException("API key required. Set OPENAI_API_KEY or pass apiKey=...");
                    currentService = new WhisperSpeechToTextService(apiKey, config.Endpoint, config.Model ?? "whisper-1");
                }
                else if (config.Provider.ToLowerInvariant() is "local" or "whisper" or "whisper.cpp" or "offline")
                {
                    // Explicitly requested local
                    currentService = new LocalWhisperService(config.WhisperPath, config.ModelPath, config.Model ?? "small");
                }
                else
                {
                    // Auto-detect: prefer local/offline, fallback to cloud
                    LocalWhisperService localService = new LocalWhisperService(config.WhisperPath, config.ModelPath, config.Model ?? "small");
                    if (await localService.IsAvailableAsync())
                    {
                        currentService = localService;
                        if (s.Trace)
                        {
                            Console.WriteLine("[stt] Using offline local Whisper");
                        }
                    }
                    else
                    {
                        // Fallback to OpenAI if available
                        string? apiKey = config.ApiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                        if (!string.IsNullOrEmpty(apiKey))
                        {
                            currentService = new WhisperSpeechToTextService(apiKey, config.Endpoint, config.Model ?? "whisper-1");
                            if (s.Trace)
                            {
                                Console.WriteLine("[stt] Local Whisper not found, using OpenAI API");
                            }
                        }
                        else
                        {
                            throw new InvalidOperationException(
                                "No speech-to-text available. Install Whisper locally or set OPENAI_API_KEY.");
                        }
                    }
                }

                if (s.Trace)
                {
                    Console.WriteLine($"[stt] Initialized {currentService.ProviderName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[stt] Failed to initialize: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Transcribe an audio file to text.
    /// Usage: Transcribe('path/to/audio.mp3')
    /// Usage: Transcribe('path/to/audio.mp3;language=en')
    /// The transcribed text is stored in s.Output and s.Context.
    /// </summary>
    [PipelineToken("Transcribe", "SttTranscribe", "AudioToText")]
    public static Step<CliPipelineState, CliPipelineState> Transcribe(string? args = null)
        => async s =>
        {
            var config = ParseTranscribeArgs(args);

            if (string.IsNullOrEmpty(config.FilePath))
            {
                Console.WriteLine("[stt] Error: File path required. Usage: Transcribe('audio.mp3')");
                return s;
            }

            if (currentService == null)
            {
                // Auto-initialize with offline-first approach
                currentService = await TryAutoInitializeAsync(s.Trace);
                if (currentService == null)
                {
                    Console.WriteLine("[stt] Error: Not initialized. Install Whisper locally or set OPENAI_API_KEY");
                    return s;
                }
            }

            Console.WriteLine($"[stt] Transcribing: {config.FilePath}");

            var options = new TranscriptionOptions(
                Language: config.Language,
                ResponseFormat: config.Format ?? "json",
                Temperature: config.Temperature,
                TimestampGranularity: config.Timestamps ? "segment" : null,
                Prompt: config.Prompt);

            var result = await currentService.TranscribeFileAsync(config.FilePath, options);

            result.Match(
                transcription =>
                {
                    s.Output = transcription.Text;
                    s.Context = transcription.Text;

                    // Always show detection info for visibility
                    Console.WriteLine();
                    Console.WriteLine($"[stt] ✓ Language detected: {transcription.Language ?? "unknown"}");
                    Console.WriteLine($"[stt] ✓ Words detected: {(string.IsNullOrWhiteSpace(transcription.Text) ? "(none)" : transcription.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length.ToString())}");
                    if (transcription.Duration.HasValue)
                    {
                        Console.WriteLine($"[stt] ✓ Duration: {transcription.Duration:F2}s");
                    }

                    Console.WriteLine();
                    Console.WriteLine("═══════════════════════════════════════════════════════");
                    Console.WriteLine($"📝 TRANSCRIPTION: {(string.IsNullOrWhiteSpace(transcription.Text) ? "[No speech detected]" : transcription.Text)}");
                    Console.WriteLine("═══════════════════════════════════════════════════════");
                    Console.WriteLine();

                    // If timestamps requested, print segments
                    if (config.Timestamps && transcription.Segments != null)
                    {
                        Console.WriteLine("[stt] Segments:");
                        foreach (var segment in transcription.Segments)
                        {
                            Console.WriteLine($"  [{segment.Start:F2}-{segment.End:F2}] {segment.Text}");
                        }
                    }
                },
                error =>
                {
                    Console.WriteLine($"[stt] Error: {error}");
                    s.Output = $"Transcription failed: {error}";
                });

            return s;
        };

    /// <summary>
    /// Translate audio to English.
    /// Usage: SttTranslate('path/to/foreign_audio.mp3')
    /// Translates non-English audio to English text.
    /// </summary>
    [PipelineToken("SttTranslate", "TranslateAudio")]
    public static Step<CliPipelineState, CliPipelineState> SttTranslate(string? args = null)
        => async s =>
        {
            var config = ParseTranscribeArgs(args);

            if (string.IsNullOrEmpty(config.FilePath))
            {
                Console.WriteLine("[stt] Error: File path required. Usage: SttTranslate('audio.mp3')");
                return s;
            }

            if (currentService == null)
            {
                currentService = await TryAutoInitializeAsync(s.Trace);
                if (currentService == null)
                {
                    Console.WriteLine("[stt] Error: Not initialized. Install Whisper locally or set OPENAI_API_KEY");
                    return s;
                }
            }

            Console.WriteLine($"[stt] Translating to English: {config.FilePath}");

            var options = new TranscriptionOptions(
                ResponseFormat: config.Format ?? "json",
                Temperature: config.Temperature,
                Prompt: config.Prompt);

            var result = await currentService.TranslateToEnglishAsync(config.FilePath, options);

            result.Match(
                transcription =>
                {
                    s.Output = transcription.Text;
                    s.Context = transcription.Text;
                    Console.WriteLine($"\n{transcription.Text}\n");
                },
                error =>
                {
                    Console.WriteLine($"[stt] Error: {error}");
                    s.Output = $"Translation failed: {error}";
                });

            return s;
        };

    /// <summary>
    /// Transcribe audio and use the result as a query for the LLM.
    /// Usage: SttAsk('audio.mp3') - transcribes and sends to LLM
    /// Combines speech-to-text with LLM processing.
    /// </summary>
    [PipelineToken("SttAsk", "VoiceAsk")]
    public static Step<CliPipelineState, CliPipelineState> SttAsk(string? args = null)
        => async s =>
        {
            var config = ParseTranscribeArgs(args);

            if (string.IsNullOrEmpty(config.FilePath))
            {
                Console.WriteLine("[stt] Error: File path required. Usage: SttAsk('audio.mp3')");
                return s;
            }

            // First transcribe (offline-first)
            if (currentService == null)
            {
                currentService = await TryAutoInitializeAsync(s.Trace);
                if (currentService == null)
                {
                    Console.WriteLine("[stt] Error: Not initialized. Install Whisper locally or set OPENAI_API_KEY");
                    return s;
                }
            }

            Console.WriteLine($"[stt] Processing voice query: {config.FilePath}");

            var options = new TranscriptionOptions(
                Language: config.Language,
                ResponseFormat: "json");

            var transcribeResult = await currentService.TranscribeFileAsync(config.FilePath, options);

            string? query = null;
            transcribeResult.Match(
                t => query = t.Text,
                error => Console.WriteLine($"[stt] Transcription error: {error}"));

            if (string.IsNullOrEmpty(query))
            {
                return s;
            }

            Console.WriteLine($"[stt] Transcribed query: {query}");

            // Set as query and call LLM
            s.Query = query;

            try
            {
                var response = await s.Llm.InnerModel.GenerateTextAsync(query);
                s.Output = response;
                Console.WriteLine($"\n[LLM Response]\n{response}\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[stt] LLM error: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Check speech-to-text service availability.
    /// Usage: SttStatus()
    /// </summary>
    [PipelineToken("SttStatus")]
    public static Step<CliPipelineState, CliPipelineState> SttStatus(string? args = null)
        => async s =>
        {
            if (currentService == null)
            {
                Console.WriteLine("[stt] Not initialized. Use SttInit() to configure.");
                return s;
            }

            Console.WriteLine($"[stt] Provider: {currentService.ProviderName}");
            Console.WriteLine($"[stt] Supported formats: {string.Join(", ", currentService.SupportedFormats)}");
            Console.WriteLine($"[stt] Max file size: {currentService.MaxFileSizeBytes / (1024 * 1024)} MB");

            var available = await currentService.IsAvailableAsync();
            Console.WriteLine($"[stt] Available: {(available ? "Yes" : "No")}");

            // Check microphone availability
            bool micAvailable = MicrophoneRecorder.IsRecordingAvailable();
            Console.WriteLine($"[stt] Microphone recording: {(micAvailable ? "Available" : "Not available (install ffmpeg)")}");

            return s;
        };
}
