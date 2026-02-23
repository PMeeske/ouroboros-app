using System.CommandLine;
using System.CommandLine.Parsing;

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

    public Option<string> QdrantOption { get; } = new("--qdrant")
    {
        Description = "Qdrant endpoint for memory storage",
        DefaultValueFactory = _ => "http://localhost:6334",
    };

    // ── Voice options ─────────────────────────────────────────────────────────
    public Option<bool> VoiceModeOption { get; } = new("--voice-mode")
    {
        Description = "Enable voice mode",
        DefaultValueFactory = _ => false,
    };

    public Option<bool> LocalTtsOption { get; } = new("--local-tts")
    {
        Description = "Use local Windows SAPI TTS instead of Azure",
        DefaultValueFactory = _ => false,
    };

    // ── Avatar options ────────────────────────────────────────────────────────
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

    // ── Room integration ──────────────────────────────────────────────────────
    public Option<bool> RoomModeOption { get; } = new("--room-mode")
    {
        Description = "Also start ambient room listening alongside immersive mode",
        DefaultValueFactory = _ => false,
    };

    /// <summary>Registers all options on <paramref name="command"/>.</summary>
    public void AddToCommand(Command command)
    {
        command.Add(PersonaOption);
        command.Add(ModelOption);
        command.Add(EndpointOption);
        command.Add(EmbedModelOption);
        command.Add(QdrantOption);
        command.Add(VoiceModeOption);
        command.Add(LocalTtsOption);
        command.Add(AvatarOption);
        command.Add(AvatarPortOption);
        command.Add(RoomModeOption);
    }

    /// <summary>
    /// Binds a <see cref="ParseResult"/> to a fully populated <see cref="ImmersiveConfig"/>.
    /// Parallels <see cref="OuroborosCommandOptions.BindConfig"/>.
    /// </summary>
    public ImmersiveConfig BindConfig(ParseResult parseResult, Option<bool>? globalVoiceOption = null)
    {
        var voice = parseResult.GetValue(VoiceModeOption);
        if (globalVoiceOption != null)
            voice = voice || parseResult.GetValue(globalVoiceOption);

        return new ImmersiveConfig(
            Persona: parseResult.GetValue(PersonaOption) ?? "Iaret",
            Model: parseResult.GetValue(ModelOption) ?? "llama3:latest",
            Endpoint: parseResult.GetValue(EndpointOption) ?? "http://localhost:11434",
            EmbedModel: parseResult.GetValue(EmbedModelOption) ?? "nomic-embed-text",
            QdrantEndpoint: parseResult.GetValue(QdrantOption) ?? "http://localhost:6334",
            Voice: voice,
            LocalTts: parseResult.GetValue(LocalTtsOption),
            Avatar: parseResult.GetValue(AvatarOption),
            AvatarPort: parseResult.GetValue(AvatarPortOption),
            RoomMode: parseResult.GetValue(RoomModeOption));
    }
}
