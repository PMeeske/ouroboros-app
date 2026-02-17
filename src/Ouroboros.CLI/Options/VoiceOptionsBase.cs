using CommandLine;

namespace Ouroboros.Options;

/// <summary>
/// Mixin class providing voice-related option defaults.
/// Commands can inherit from this or implement IVoiceOptions directly.
/// </summary>
public abstract class VoiceOptionsBase : IVoiceOptions
{
    [Option('v', "voice", Required = false, Default = false, HelpText = "Enable voice persona mode (speak & listen).")]
    public bool Voice { get; set; }

    [Option("persona", Required = false, Default = "Iaret", HelpText = "Persona name for voice mode (Iaret, Aria, Nova, Echo, Sage, Atlas).")]
    public string Persona { get; set; } = "Iaret";

    [Option("model", Required = false, Default = "ministral-3:latest", HelpText = "LLM model for voice mode.")]
    public virtual string Model { get; set; } = "ministral-3:latest";

    [Option("endpoint", Required = false, Default = "http://localhost:11434", HelpText = "LLM endpoint URL.")]
    public virtual string Endpoint { get; set; } = "http://localhost:11434";

    [Option("embed-model", Required = false, Default = "nomic-embed-text", HelpText = "Embedding model for semantic search.")]
    public virtual string EmbedModel { get; set; } = "nomic-embed-text";

    [Option("qdrant", Required = false, Default = "http://localhost:6334", HelpText = "Qdrant endpoint for skill storage.")]
    public virtual string QdrantEndpoint { get; set; } = "http://localhost:6334";

    [Option("voice-only", Required = false, Default = false, HelpText = "Voice-only mode (no text output).")]
    public bool VoiceOnly { get; set; }

    [Option("local-tts", Required = false, Default = true, HelpText = "Prefer local TTS (Windows SAPI) over cloud.")]
    public bool LocalTts { get; set; } = true;

    [Option("voice-loop", Required = false, Default = false, HelpText = "Continue voice conversation after command.")]
    public bool VoiceLoop { get; set; }
}