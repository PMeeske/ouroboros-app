// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Commands.Options;

using System.CommandLine;
using System.CommandLine.Parsing;

/// <summary>
/// Options for the <c>room</c> subcommand — Iaret as ambient room presence.
///
/// Wires <see cref="AmbientRoomListener"/> to the microphone and runs the
/// five-stage interjection pipeline (Ethics → CogPhysics → Phi → LLM → TTS).
/// </summary>
public sealed class RoomCommandOptions
{
    // ── Persona & model ─────────────────────────────────────────────────────
    public Option<string> PersonaOption { get; } = new("--persona")
    {
        Description = "Persona name (default: Iaret)",
        DefaultValueFactory = _ => "Iaret",
    };

    public Option<string> ModelOption { get; } = new("--model")
    {
        Description = "LLM model used for interjection decisions",
        DefaultValueFactory = _ => "llama3:latest",
    };

    public Option<string> EndpointOption { get; } = new("--endpoint")
    {
        Description = "LLM endpoint URL",
        DefaultValueFactory = _ => "http://localhost:11434",
    };

    public Option<string> EmbedModelOption { get; } = new("--embed-model")
    {
        Description = "Embedding model for semantic memory",
        DefaultValueFactory = _ => "nomic-embed-text",
    };

    public Option<string> QdrantEndpointOption { get; } = new("--qdrant")
    {
        Description = "Qdrant endpoint for person/conversation memory",
        DefaultValueFactory = _ => "http://localhost:6334",
    };

    // ── Speech ───────────────────────────────────────────────────────────────
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

    public Option<bool> LocalTtsOption { get; } = new("--local-tts")
    {
        Description = "Use local Windows SAPI TTS instead of Azure",
        DefaultValueFactory = _ => false,
    };

    // ── Avatar ───────────────────────────────────────────────────────────────
    public Option<bool> AvatarOption { get; } = new("--avatar")
    {
        Description = "Launch the interactive avatar viewer",
        DefaultValueFactory = _ => true,
    };

    public Option<int> AvatarPortOption { get; } = new("--avatar-port")
    {
        Description = "Port for the avatar viewer WebSocket",
        DefaultValueFactory = _ => 9471,
    };

    // ── Room-specific ─────────────────────────────────────────────────────────
    public Option<bool> QuietOption { get; } = new("--quiet", "-q")
    {
        Description = "Don't announce Iaret's arrival via TTS",
        DefaultValueFactory = _ => false,
    };

    public Option<int> CooldownOption { get; } = new("--cooldown")
    {
        Description = "Minimum seconds between interjections (default: 20)",
        DefaultValueFactory = _ => 20,
    };

    public Option<int> MaxInterjectionsOption { get; } = new("--max-interjections")
    {
        Description = "Maximum interjections per 10-minute window (default: 8)",
        DefaultValueFactory = _ => 8,
    };

    public Option<double> PhiThresholdOption { get; } = new("--phi-threshold")
    {
        Description = "Minimum Phi (IIT integration) score before Iaret considers interjecting (0.0–1.0, default: 0.05)",
        DefaultValueFactory = _ => 0.05,
    };

    public Option<bool> ProactiveOption { get; } = new("--proactive")
    {
        Description = "Enable proactive speech when room is idle (default: true)",
        DefaultValueFactory = _ => true,
    };

    public Option<int> IdleDelayOption { get; } = new("--idle-delay")
    {
        Description = "Seconds of silence before proactive speech (default: 120)",
        DefaultValueFactory = _ => 120,
    };

    public Option<bool> CameraOption { get; } = new("--camera")
    {
        Description = "Enable camera-based presence and gesture detection (default: false, privacy safeguard)",
        DefaultValueFactory = _ => false,
    };

    /// <summary>Registers all options on <paramref name="command"/>.</summary>
    public void AddToCommand(Command command)
    {
        command.Add(PersonaOption);
        command.Add(ModelOption);
        command.Add(EndpointOption);
        command.Add(EmbedModelOption);
        command.Add(QdrantEndpointOption);
        command.Add(AzureSpeechKeyOption);
        command.Add(AzureSpeechRegionOption);
        command.Add(TtsVoiceOption);
        command.Add(LocalTtsOption);
        command.Add(AvatarOption);
        command.Add(AvatarPortOption);
        command.Add(QuietOption);
        command.Add(CooldownOption);
        command.Add(MaxInterjectionsOption);
        command.Add(PhiThresholdOption);
        command.Add(ProactiveOption);
        command.Add(IdleDelayOption);
        command.Add(CameraOption);
    }

    /// <summary>
    /// Binds a <see cref="ParseResult"/> to a fully populated <see cref="RoomConfig"/>.
    /// Parallels <see cref="OuroborosCommandOptions.BindConfig"/>.
    /// </summary>
    public RoomConfig BindConfig(ParseResult parseResult)
    {
        var speechKey = parseResult.GetValue(AzureSpeechKeyOption)
                        ?? Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");

        return new RoomConfig(
            Persona:           parseResult.GetValue(PersonaOption) ?? "Iaret",
            Model:             parseResult.GetValue(ModelOption) ?? "llama3:latest",
            Endpoint:          parseResult.GetValue(EndpointOption) ?? "http://localhost:11434",
            EmbedModel:        parseResult.GetValue(EmbedModelOption) ?? "nomic-embed-text",
            QdrantEndpoint:    parseResult.GetValue(QdrantEndpointOption) ?? "http://localhost:6334",
            AzureSpeechKey:    speechKey,
            AzureSpeechRegion: parseResult.GetValue(AzureSpeechRegionOption) ?? "eastus",
            TtsVoice:          parseResult.GetValue(TtsVoiceOption) ?? "en-US-AvaMultilingualNeural",
            LocalTts:          parseResult.GetValue(LocalTtsOption),
            Avatar:            parseResult.GetValue(AvatarOption),
            AvatarPort:        parseResult.GetValue(AvatarPortOption),
            Quiet:             parseResult.GetValue(QuietOption),
            CooldownSeconds:   parseResult.GetValue(CooldownOption),
            MaxInterjections:  parseResult.GetValue(MaxInterjectionsOption),
            PhiThreshold:      parseResult.GetValue(PhiThresholdOption),
            Proactive:         parseResult.GetValue(ProactiveOption),
            IdleDelaySeconds:  parseResult.GetValue(IdleDelayOption),
            EnableCamera:      parseResult.GetValue(CameraOption));
    }
}
