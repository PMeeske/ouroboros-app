using System.CommandLine;
using System.CommandLine.Parsing;
using Ouroboros.Application.Configuration;

namespace Ouroboros.CLI.Commands.Options;

/// <summary>
/// Options for the <c>immersive</c> subcommand.
/// Follows the same pattern as <see cref="OuroborosCommandOptions"/>:
/// defines System.CommandLine options, registers them, and binds to <see cref="ImmersiveConfig"/>.
/// </summary>
public sealed class ImmersiveCommandOptions
{
    // ── Core model options ────────────────────────────────────────────────────
    public Option<string> PersonaOption { get; } = new("--persona")
    {
        Description = "Persona name (default: Iaret)",
        DefaultValueFactory = _ => "Iaret",
    };

    public Option<string> ModelOption { get; } = new("--model")
    {
        Description = "LLM model to use",
        DefaultValueFactory = _ => "deepseek-v3.1:671b-cloud",
    };

    public Option<string> EndpointOption { get; } = new("--endpoint")
    {
        Description = "LLM endpoint URL",
        DefaultValueFactory = _ => DefaultEndpoints.Ollama,
    };

    public Option<string> EmbedModelOption { get; } = new("--embed-model")
    {
        Description = "Embedding model for semantic memory",
        DefaultValueFactory = _ => "nomic-embed-text",
    };

    public Option<string> QdrantEndpointOption { get; } = new("--qdrant")
    {
        Description = "Qdrant endpoint for vector memory",
        DefaultValueFactory = _ => DefaultEndpoints.QdrantGrpc,
    };

    // ── Voice options ─────────────────────────────────────────────────────────
    public Option<bool> VoiceModeOption { get; } = new("--voice-mode")
    {
        Description = "Enable voice interaction",
        DefaultValueFactory = _ => false,
    };

    public Option<bool> VoiceOnlyOption { get; } = new("--voice-only")
    {
        Description = "Voice-only mode (no text display)",
        DefaultValueFactory = _ => false,
    };

    public Option<bool> LocalTtsOption { get; } = new("--local-tts")
    {
        Description = "Use local Windows SAPI TTS instead of Azure",
        DefaultValueFactory = _ => false,
    };

    public Option<bool> AzureTtsOption { get; } = new("--azure-tts")
    {
        Description = "Use Azure Neural TTS (default: true)",
        DefaultValueFactory = _ => true,
    };

    public Option<string?> AzureSpeechKeyOption { get; } = new("--azure-speech-key")
    {
        Description = "Azure Speech API key (or set AZURE_SPEECH_KEY env var)",
    };

    public Option<string> AzureSpeechRegionOption { get; } = new("--azure-speech-region")
    {
        Description = "Azure Speech region",
        DefaultValueFactory = _ => "eastus",
    };

    public Option<string> TtsVoiceOption { get; } = new("--tts-voice")
    {
        Description = "TTS voice name",
        DefaultValueFactory = _ => "en-US-AvaMultilingualNeural",
    };

    // ── Avatar options ────────────────────────────────────────────────────────
    public Option<bool> AvatarOption { get; } = new("--avatar")
    {
        Description = "Launch the interactive avatar viewer",
        DefaultValueFactory = _ => true,
    };

    public Option<bool> AvatarCloudOption { get; } = new("--avatar-cloud")
    {
        Description = "Enable Stability AI cloud frame generation (requires credits)",
        DefaultValueFactory = _ => false,
    };

    public Option<int> AvatarPortOption { get; } = new("--avatar-port")
    {
        Description = "Port for the avatar viewer WebSocket",
        DefaultValueFactory = _ => 9471,
    };

    // ── Room integration ──────────────────────────────────────────────────────
    public Option<bool> RoomModeOption { get; } = new("--room-mode")
    {
        Description = "Also start ambient room listening alongside immersive mode",
        DefaultValueFactory = _ => false,
    };

    // ── OpenClaw Gateway ────────────────────────────────────────────────────
    public Option<bool> EnableOpenClawOption { get; } = new("--enable-openclaw")
    {
        Description = "Connect to the OpenClaw Gateway for messaging and device node integration",
        DefaultValueFactory = _ => true,
    };

    public Option<string?> OpenClawGatewayOption { get; } = new("--openclaw-gateway")
    {
        Description = "OpenClaw Gateway WebSocket URL (default: ws://127.0.0.1:18789)",
    };

    public Option<string?> OpenClawTokenOption { get; } = new("--openclaw-token")
    {
        Description = "OpenClaw Gateway auth token (or set OPENCLAW_TOKEN env var / user-secrets)",
    };

    /// <summary>Registers all options on <paramref name="command"/>.</summary>
    public void AddToCommand(Command command)
    {
        command.Add(PersonaOption);
        command.Add(ModelOption);
        command.Add(EndpointOption);
        command.Add(EmbedModelOption);
        command.Add(QdrantEndpointOption);
        command.Add(VoiceModeOption);
        command.Add(VoiceOnlyOption);
        command.Add(LocalTtsOption);
        command.Add(AzureTtsOption);
        command.Add(AzureSpeechKeyOption);
        command.Add(AzureSpeechRegionOption);
        command.Add(TtsVoiceOption);
        command.Add(AvatarOption);
        command.Add(AvatarCloudOption);
        command.Add(AvatarPortOption);
        command.Add(RoomModeOption);
        command.Add(EnableOpenClawOption);
        command.Add(OpenClawGatewayOption);
        command.Add(OpenClawTokenOption);
    }

    /// <summary>
    /// Binds a <see cref="ParseResult"/> to a fully populated <see cref="ImmersiveConfig"/>.
    /// Parallels <see cref="OuroborosCommandOptions.BindConfig"/>.
    /// </summary>
    public ImmersiveConfig BindConfig(ParseResult parseResult, Option<bool>? globalVoiceOption = null)
    {
        var localTts = parseResult.GetValue(LocalTtsOption);
        var speechKey = parseResult.GetValue(AzureSpeechKeyOption)
                        ?? Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
        var azureTts = localTts ? false : (parseResult.GetValue(AzureTtsOption) && !string.IsNullOrEmpty(speechKey));

        var voice = parseResult.GetValue(VoiceModeOption);
        if (globalVoiceOption != null)
            voice = voice || parseResult.GetValue(globalVoiceOption);

        return new ImmersiveConfig(
            Persona:           parseResult.GetValue(PersonaOption) ?? "Iaret",
            Model:             parseResult.GetValue(ModelOption) ?? "deepseek-v3.1:671b-cloud",
            Endpoint:          parseResult.GetValue(EndpointOption) ?? DefaultEndpoints.Ollama,
            EmbedModel:        parseResult.GetValue(EmbedModelOption) ?? "nomic-embed-text",
            QdrantEndpoint:    parseResult.GetValue(QdrantEndpointOption) ?? DefaultEndpoints.QdrantGrpc,
            Voice:             voice,
            VoiceOnly:         parseResult.GetValue(VoiceOnlyOption),
            LocalTts:          localTts,
            AzureTts:          azureTts,
            AzureSpeechKey:    speechKey,
            AzureSpeechRegion: parseResult.GetValue(AzureSpeechRegionOption) ?? "eastus",
            TtsVoice:          parseResult.GetValue(TtsVoiceOption) ?? "en-US-AvaMultilingualNeural",
            Avatar:            parseResult.GetValue(AvatarOption),
            AvatarCloud:       parseResult.GetValue(AvatarCloudOption),
            AvatarPort:        parseResult.GetValue(AvatarPortOption),
            RoomMode:          parseResult.GetValue(RoomModeOption),
            EnableOpenClaw:    parseResult.GetValue(EnableOpenClawOption),
            OpenClawGateway:   parseResult.GetValue(OpenClawGatewayOption),
            OpenClawToken:     parseResult.GetValue(OpenClawTokenOption)
                               ?? Environment.GetEnvironmentVariable("OPENCLAW_TOKEN"));
    }
}
