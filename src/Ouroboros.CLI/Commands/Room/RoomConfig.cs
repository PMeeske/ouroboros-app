namespace Ouroboros.CLI.Commands;

/// <summary>
/// Configuration for the room presence command.
/// Maps CLI options from <see cref="Options.RoomCommandOptions"/> to a
/// strongly-typed record consumed by <see cref="Handlers.RoomCommandHandler"/>.
/// Mirrors the <see cref="OuroborosConfig"/> pattern.
/// </summary>
public sealed record RoomConfig(
    string Persona = "Iaret",
    string Model = "llama3:latest",
    string Endpoint = "http://localhost:11434",
    string EmbedModel = "nomic-embed-text",
    string QdrantEndpoint = "http://localhost:6334",
    string? AzureSpeechKey = null,
    string AzureSpeechRegion = "eastus",
    string TtsVoice = "en-US-AvaMultilingualNeural",
    bool LocalTts = false,
    bool Avatar = true,
    int AvatarPort = 9471,
    bool Quiet = false,
    int CooldownSeconds = 45,
    int MaxInterjections = 4,
    double PhiThreshold = 0.15);
