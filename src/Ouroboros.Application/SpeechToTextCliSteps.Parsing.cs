// <copyright file="SpeechToTextCliSteps.Parsing.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Providers.SpeechToText;

namespace Ouroboros.Application;

/// <summary>
/// Partial class containing argument parsing helpers and configuration types.
/// </summary>
public static partial class SpeechToTextCliSteps
{
    #region Helpers

    /// <summary>
    /// Auto-initialize STT service with offline-first approach.
    /// Tries local Whisper first, falls back to OpenAI if available.
    /// </summary>
    private static async Task<ISpeechToTextService?> TryAutoInitializeAsync(bool trace)
    {
        // Try local Whisper first (offline-first)
        LocalWhisperService localService = new LocalWhisperService();
        if (await localService.IsAvailableAsync())
        {
            if (trace)
            {
                Console.WriteLine("[stt] Auto-initialized local Whisper (offline)");
            }

            return localService;
        }

        // Fallback to OpenAI if API key is available
        string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrEmpty(apiKey))
        {
            if (trace)
            {
                Console.WriteLine("[stt] Local Whisper not found, using OpenAI API");
            }

            return new WhisperSpeechToTextService(apiKey);
        }

        return null;
    }

    #endregion

    #region Argument Parsing

    private sealed class SttConfig
    {
        public string Provider { get; set; } = "auto";

        public string? ApiKey { get; set; }

        public string? Endpoint { get; set; }

        public string? Model { get; set; }

        public string? WhisperPath { get; set; }

        public string? ModelPath { get; set; }
    }

    private sealed class TranscribeConfig
    {
        public string? FilePath { get; set; }
        public string? Language { get; set; }
        public string? Format { get; set; }
        public double? Temperature { get; set; }
        public bool Timestamps { get; set; }
        public string? Prompt { get; set; }
    }

    private sealed class RecordConfig
    {
        public int Duration { get; set; }

        public string? OutputPath { get; set; }

        public string Format { get; set; } = "wav";

        public int MaxDuration { get; set; } = 300;

        public string? Language { get; set; }
    }

    private static SttConfig ParseSttConfig(string? args)
    {
        var config = new SttConfig();
        if (string.IsNullOrWhiteSpace(args)) return config;

        // Remove surrounding quotes
        args = args.Trim().Trim('\'', '"');

        foreach (var part in args.Split(';'))
        {
            var trimmed = part.Trim();
            var eqIndex = trimmed.IndexOf('=');

            if (eqIndex > 0)
            {
                var key = trimmed[..eqIndex].Trim().ToLowerInvariant();
                var value = trimmed[(eqIndex + 1)..].Trim();

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
                    case "whisperpath" or "path":
                        config.WhisperPath = value;
                        break;
                    case "modelpath":
                        config.ModelPath = value;
                        break;
                }
            }
            else if (!string.IsNullOrEmpty(trimmed))
            {
                // First non-key=value is provider
                config.Provider = trimmed;
            }
        }

        return config;
    }

    private static TranscribeConfig ParseTranscribeArgs(string? args)
    {
        var config = new TranscribeConfig();
        if (string.IsNullOrWhiteSpace(args)) return config;

        args = args.Trim().Trim('\'', '"');

        foreach (var part in args.Split(';'))
        {
            var trimmed = part.Trim();
            var eqIndex = trimmed.IndexOf('=');

            if (eqIndex > 0)
            {
                var key = trimmed[..eqIndex].Trim().ToLowerInvariant();
                var value = trimmed[(eqIndex + 1)..].Trim();

                switch (key)
                {
                    case "language" or "lang":
                        config.Language = value;
                        break;
                    case "format":
                        config.Format = value;
                        break;
                    case "temperature" or "temp":
                        if (double.TryParse(value, out var temp))
                            config.Temperature = temp;
                        break;
                    case "timestamps":
                        config.Timestamps = value.ToLowerInvariant() is "true" or "1" or "yes";
                        break;
                    case "prompt":
                        config.Prompt = value;
                        break;
                    case "file" or "path":
                        config.FilePath = value;
                        break;
                }
            }
            else if (!string.IsNullOrEmpty(trimmed) && config.FilePath == null)
            {
                // First non-key=value is file path
                config.FilePath = trimmed;
            }
        }

        return config;
    }

    private static RecordConfig ParseRecordArgs(string? args)
    {
        RecordConfig config = new RecordConfig();
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
                    case "duration" or "time" or "seconds":
                        if (int.TryParse(value, out int dur))
                        {
                            config.Duration = dur;
                        }

                        break;
                    case "output" or "out" or "path" or "file":
                        config.OutputPath = value;
                        break;
                    case "format":
                        config.Format = value;
                        break;
                    case "max" or "maxduration":
                        if (int.TryParse(value, out int maxDur))
                        {
                            config.MaxDuration = maxDur;
                        }

                        break;
                    case "language" or "lang":
                        config.Language = value;
                        break;
                }
            }
            else if (!string.IsNullOrEmpty(trimmed) && int.TryParse(trimmed, out int duration))
            {
                // First non-key=value number is duration
                config.Duration = duration;
            }
        }

        return config;
    }

    #endregion
}
