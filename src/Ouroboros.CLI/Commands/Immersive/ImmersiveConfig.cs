namespace Ouroboros.CLI.Commands;

/// <summary>
/// Configuration for the immersive persona mode.
/// Mirrors <see cref="OuroborosConfig"/> style as an immutable record
/// carrying all CLI-parsed values for the immersive session.
/// </summary>
public sealed record ImmersiveConfig(
    string Persona = "Iaret",
    string Model = "llama3:latest",
    string Endpoint = "http://localhost:11434",
    string EmbedModel = "nomic-embed-text",
    string QdrantEndpoint = "http://localhost:6334",
    bool Voice = false,
    bool VoiceOnly = false,
    bool LocalTts = false,
    bool VoiceLoop = false,
    bool Avatar = true,
    int AvatarPort = 9471,
    bool RoomMode = false,
    bool AzureTts = true,
    string TtsVoice = "en-US-AvaMultilingualNeural",
    string? AzureSpeechKey = null,
    string AzureSpeechRegion = "eastus");
