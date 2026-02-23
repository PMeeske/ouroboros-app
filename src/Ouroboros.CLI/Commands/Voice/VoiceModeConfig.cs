namespace Ouroboros.CLI.Commands;

/// <summary>
/// Configuration for voice mode.
/// </summary>
public sealed record VoiceModeConfig(
    string Persona = "Iaret",
    bool VoiceOnly = false,
    /// Defaults to false (prefer cloud TTS for higher quality).
    /// Use --local-tts for offline/low-latency scenarios.
    bool LocalTts = false,
    bool VoiceLoop = true,
    bool DisableStt = false,
    string Model = "llama3",
    string Endpoint = "http://localhost:11434",
    string EmbedModel = "nomic-embed-text",
    string QdrantEndpoint = "http://localhost:6334",
    string? Culture = null);
