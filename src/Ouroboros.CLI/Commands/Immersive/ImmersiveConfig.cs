using Ouroboros.Application.Configuration;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Configuration for the immersive persona mode.
/// Mirrors <see cref="OuroborosConfig"/> style as an immutable record
/// carrying all CLI-parsed values for the immersive session.
/// </summary>
public sealed record ImmersiveConfig(
    string Persona = "Iaret",
    string Model = "deepseek-v3.1:671b-cloud",
    string Endpoint = DefaultEndpoints.Ollama,
    string EmbedModel = "nomic-embed-text",
    string QdrantEndpoint = DefaultEndpoints.QdrantGrpc,
    bool Voice = false,
    bool VoiceOnly = false,
    bool LocalTts = false,
    bool AzureTts = true,
    string? AzureSpeechKey = null,
    string AzureSpeechRegion = "eastus",
    string TtsVoice = "en-US-AvaMultilingualNeural",
    bool Avatar = true,
    bool AvatarCloud = false,
    int AvatarPort = 9471,
    bool RoomMode = false,
    // OpenClaw Gateway
    bool EnableOpenClaw = true,
    string? OpenClawGateway = null,
    string? OpenClawToken = null);
