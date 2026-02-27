// <copyright file="SpeechToTextCliSteps.Recording.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Providers.SpeechToText;

namespace Ouroboros.Application;

/// <summary>
/// Partial class containing microphone recording and voice interaction pipeline steps.
/// </summary>
public static partial class SpeechToTextCliSteps
{
    /// <summary>
    /// Record audio from microphone for a specified duration.
    /// Usage: Record('5') - records for 5 seconds
    /// Usage: Record('10;output=my_recording.wav')
    /// The recorded file path is stored in s.Output.
    /// </summary>
    [PipelineToken("Record", "RecordMic", "MicRecord")]
    public static Step<CliPipelineState, CliPipelineState> Record(string? args = null)
        => async s =>
        {
            RecordConfig config = ParseRecordArgs(args);

            if (config.Duration <= 0)
            {
                Console.WriteLine("[stt] Error: Duration required. Usage: Record('5') for 5 seconds");
                return s;
            }

            if (!MicrophoneRecorder.IsRecordingAvailable())
            {
                Console.WriteLine("[stt] Error: No audio recorder available. Please install ffmpeg.");
                return s;
            }

            Console.WriteLine($"[stt] 🎤 Recording for {config.Duration} seconds...");

            Result<string, string> result = await MicrophoneRecorder.RecordAsync(
                config.Duration,
                config.OutputPath,
                config.Format);

            result.Match(
                path =>
                {
                    Console.WriteLine($"[stt] ✓ Recorded to: {path}");
                    s.Output = path;
                    s.Context = path;
                },
                error => Console.WriteLine($"[stt] Error: {error}"));

            return s;
        };

    /// <summary>
    /// Record audio from microphone until Enter is pressed.
    /// Usage: Listen() - starts recording, press Enter to stop
    /// Usage: Listen('output=recording.wav')
    /// The recorded file path is stored in s.Output.
    /// </summary>
    [PipelineToken("Listen", "ListenMic", "StartRecording")]
    public static Step<CliPipelineState, CliPipelineState> Listen(string? args = null)
        => async s =>
        {
            RecordConfig config = ParseRecordArgs(args);

            if (!MicrophoneRecorder.IsRecordingAvailable())
            {
                Console.WriteLine("[stt] Error: No audio recorder available. Please install ffmpeg.");
                return s;
            }

            Result<string, string> result = await MicrophoneRecorder.RecordUntilKeyPressAsync(
                config.OutputPath,
                config.Format,
                config.MaxDuration);

            result.Match(
                path =>
                {
                    Console.WriteLine($"[stt] ✓ Recorded to: {path}");
                    s.Output = path;
                    s.Context = path;
                },
                error => Console.WriteLine($"[stt] Error: {error}"));

            return s;
        };

    /// <summary>
    /// Record from microphone and transcribe immediately.
    /// Usage: Dictate('5') - records for 5 seconds and transcribes
    /// Usage: Dictate() - records until Enter, then transcribes
    /// The transcription is stored in s.Output.
    /// </summary>
    [PipelineToken("Dictate", "VoiceInput", "SpeakToText")]
    public static Step<CliPipelineState, CliPipelineState> Dictate(string? args = null)
        => async s =>
        {
            RecordConfig config = ParseRecordArgs(args);

            if (!MicrophoneRecorder.IsRecordingAvailable())
            {
                Console.WriteLine("[stt] Error: No audio recorder available. Please install ffmpeg.");
                return s;
            }

            // Initialize STT if needed (offline-first)
            if (currentService == null)
            {
                currentService = await TryAutoInitializeAsync(s.Trace);
                if (currentService == null)
                {
                    Console.WriteLine("[stt] Error: Not initialized. Install Whisper locally or set OPENAI_API_KEY");
                    return s;
                }
            }

            // Record
            Result<string, string> recordResult;
            if (config.Duration > 0)
            {
                Console.WriteLine($"[stt] 🎤 Recording for {config.Duration} seconds...");
                recordResult = await MicrophoneRecorder.RecordAsync(config.Duration, null, "wav");
            }
            else
            {
                recordResult = await MicrophoneRecorder.RecordUntilKeyPressAsync(null, "wav");
            }

            string? audioPath = null;
            recordResult.Match(
                path => audioPath = path,
                error => Console.WriteLine($"[stt] Recording error: {error}"));

            if (string.IsNullOrEmpty(audioPath))
            {
                return s;
            }

            // Transcribe
            Console.WriteLine("[stt] Transcribing...");

            TranscriptionOptions options = new TranscriptionOptions(
                Language: config.Language,
                ResponseFormat: "json");

            Result<TranscriptionResult, string> transcribeResult = await currentService.TranscribeFileAsync(audioPath, options);

            transcribeResult.Match(
                transcription =>
                {
                    s.Output = transcription.Text;
                    s.Context = transcription.Text;

                    // Show detected language and text
                    Console.WriteLine();
                    Console.WriteLine($"[stt] ✓ Language detected: {transcription.Language ?? "unknown"}");
                    Console.WriteLine($"[stt] ✓ Words detected: {(string.IsNullOrWhiteSpace(transcription.Text) ? "(none - silence or no speech)" : transcription.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length.ToString())}");
                    Console.WriteLine();
                    Console.WriteLine("═══════════════════════════════════════════════════════");
                    Console.WriteLine($"📝 TRANSCRIPTION: {(string.IsNullOrWhiteSpace(transcription.Text) ? "[No speech detected]" : transcription.Text)}");
                    Console.WriteLine("═══════════════════════════════════════════════════════");
                    Console.WriteLine();

                    // Show segments if available
                    if (transcription.Segments?.Count > 0)
                    {
                        Console.WriteLine("[stt] Segments:");
                        foreach (var seg in transcription.Segments)
                        {
                            Console.WriteLine($"  [{seg.Start:F1}s - {seg.End:F1}s] \"{seg.Text}\"");
                        }
                        Console.WriteLine();
                    }
                },
                error => Console.WriteLine($"[stt] Transcription error: {error}"));

            // Clean up temp file
            try
            {
                if (File.Exists(audioPath) && audioPath.Contains(Path.GetTempPath()))
                {
                    File.Delete(audioPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }

            return s;
        };

    /// <summary>
    /// Record from microphone, transcribe, and ask the LLM.
    /// Usage: VoiceChat('5') - records 5 seconds, transcribes, sends to LLM
    /// Usage: VoiceChat() - records until Enter, transcribes, sends to LLM
    /// Combines voice input with LLM processing.
    /// </summary>
    [PipelineToken("VoiceChat", "TalkToAi", "VoiceQuery")]
    public static Step<CliPipelineState, CliPipelineState> VoiceChat(string? args = null)
        => async s =>
        {
            RecordConfig config = ParseRecordArgs(args);

            if (!MicrophoneRecorder.IsRecordingAvailable())
            {
                Console.WriteLine("[stt] Error: No audio recorder available. Please install ffmpeg.");
                return s;
            }

            // Initialize STT if needed (offline-first)
            if (currentService == null)
            {
                currentService = await TryAutoInitializeAsync(s.Trace);
                if (currentService == null)
                {
                    Console.WriteLine("[stt] Error: Not initialized. Install Whisper locally or set OPENAI_API_KEY");
                    return s;
                }
            }

            // Record
            Result<string, string> recordResult;
            if (config.Duration > 0)
            {
                Console.WriteLine($"[stt] 🎤 Recording for {config.Duration} seconds...");
                recordResult = await MicrophoneRecorder.RecordAsync(config.Duration, null, "wav");
            }
            else
            {
                recordResult = await MicrophoneRecorder.RecordUntilKeyPressAsync(null, "wav");
            }

            string? audioPath = null;
            recordResult.Match(
                path => audioPath = path,
                error => Console.WriteLine($"[stt] Recording error: {error}"));

            if (string.IsNullOrEmpty(audioPath))
            {
                return s;
            }

            // Transcribe
            Console.WriteLine("[stt] Transcribing...");

            TranscriptionOptions options = new TranscriptionOptions(Language: config.Language);
            Result<TranscriptionResult, string> transcribeResult = await currentService.TranscribeFileAsync(audioPath, options);

            string? query = null;
            transcribeResult.Match(
                t =>
                {
                    query = t.Text;
                    Console.WriteLine();
                    Console.WriteLine($"[stt] ✓ Language: {t.Language ?? "auto"}");
                    Console.WriteLine($"[stt] ✓ Words detected: {(string.IsNullOrWhiteSpace(t.Text) ? "(none - silence)" : t.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length.ToString())}");
                    Console.WriteLine();
                    Console.WriteLine("═══════════════════════════════════════════════════════");
                    Console.WriteLine($"📝 YOU SAID: {(string.IsNullOrWhiteSpace(t.Text) ? "[No speech detected]" : t.Text)}");
                    Console.WriteLine("═══════════════════════════════════════════════════════");
                    Console.WriteLine();
                },
                error => Console.WriteLine($"[stt] Transcription error: {error}"));

            // Clean up temp file
            try
            {
                if (File.Exists(audioPath) && audioPath.Contains(Path.GetTempPath()))
                {
                    File.Delete(audioPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }

            if (string.IsNullOrEmpty(query))
            {
                return s;
            }

            // Ask LLM
            s.Query = query;

            try
            {
                string response = await s.Llm.InnerModel.GenerateTextAsync(query);
                s.Output = response;
                Console.WriteLine($"\n[LLM] {response}\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[stt] LLM error: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// List available audio recording devices.
    /// Usage: MicDevices()
    /// </summary>
    [PipelineToken("MicDevices", "ListMics", "AudioDevices")]
    public static Step<CliPipelineState, CliPipelineState> MicDevices(string? args = null)
        => async s =>
        {
            Console.WriteLine("[stt] Audio recording devices:");
            string devices = await MicrophoneRecorder.GetDeviceInfoAsync();
            Console.WriteLine(devices);
            return s;
        };
}
