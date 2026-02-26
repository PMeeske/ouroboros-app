namespace Ouroboros.CLI.Commands;

/// <summary>
/// Configuration for the room presence mode.
/// Immutable record carrying all CLI-parsed values for an ambient room session.
/// Note: Room mode is always-listening (ambient presence) by design.
/// Voice is inherently active — there is no --voice flag because Room
/// relies on continuous speech-to-text for ambient awareness. The LocalTts
/// flag controls only the TTS output method, not whether voice is enabled.
/// </summary>
public sealed record RoomConfig(
    string Persona = "Iaret",
    string Model = "deepseek-v3.1:671b-cloud",
    string Endpoint = "http://localhost:11434",
    string EmbedModel = "nomic-embed-text",
    string QdrantEndpoint = "http://localhost:6334",
    string? AzureSpeechKey = null,
    string AzureSpeechRegion = "eastus",
    string TtsVoice = "en-US-AvaMultilingualNeural",
    bool LocalTts = false,
    bool Avatar = true,
    bool AvatarCloud = false,
    int AvatarPort = 9471,
    bool Quiet = false,
    int CooldownSeconds = 20,
    int MaxInterjections = 8,
    double PhiThreshold = 0.05,
    bool Proactive = true,
    int IdleDelaySeconds = 120,
    bool EnableCamera = false);
