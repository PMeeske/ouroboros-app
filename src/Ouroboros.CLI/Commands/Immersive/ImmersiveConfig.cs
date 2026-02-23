namespace Ouroboros.CLI.Commands;

/// <summary>
/// Configuration for the immersive persona command.
/// Maps CLI options from <see cref="Options.ImmersiveCommandOptions"/> to a
/// strongly-typed record consumed by <see cref="Handlers.ImmersiveCommandHandler"/>.
/// Mirrors the <see cref="OuroborosConfig"/> pattern.
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
    bool AzureTts = true,
    string? AzureSpeechKey = null,
    string AzureSpeechRegion = "eastus",
    string TtsVoice = "en-US-AvaMultilingualNeural",
    bool Avatar = true,
    int AvatarPort = 9471,
    bool RoomMode = false);
