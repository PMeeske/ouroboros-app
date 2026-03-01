using Ouroboros.Application.Configuration;

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
    string Endpoint = DefaultEndpoints.Ollama,
    string EmbedModel = "nomic-embed-text",
    string QdrantEndpoint = DefaultEndpoints.QdrantGrpc,
    string? Culture = null);
