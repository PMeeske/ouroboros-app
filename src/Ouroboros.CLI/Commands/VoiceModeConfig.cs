namespace Ouroboros.CLI.Commands;

/// <summary>
/// Configuration for voice mode.
/// </summary>
public sealed record VoiceModeConfig(
    string Persona = "Jenny",
    bool VoiceOnly = false,
    bool LocalTts = true,
    bool VoiceLoop = true,
    bool DisableStt = false,
    string Model = "llama3",
    string Endpoint = "http://localhost:11434",
    string EmbedModel = "nomic-embed-text",
    string QdrantEndpoint = "http://localhost:6334",
    string? Culture = null);