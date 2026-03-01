using System.CommandLine;
using Ouroboros.Application.Configuration;

namespace Ouroboros.CLI.Commands.Options;

/// <summary>
/// Base class for voice-related command options using System.CommandLine 2.0.3 GA
/// </summary>
public class VoiceCommandOptions
{
    public System.CommandLine.Option<bool> VoiceOption { get; } = new("--voice")
    {
        Description = "Enable voice persona mode (speak & listen)",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<string> PersonaOption { get; } = new("--persona")
    {
        Description = "Persona name for voice mode",
        DefaultValueFactory = _ => "Ouroboros"
    };

    public System.CommandLine.Option<string> EmbedModelOption { get; } = new("--embed-model")
    {
        Description = "Embedding model for voice mode",
        DefaultValueFactory = _ => "nomic-embed-text"
    };

    public System.CommandLine.Option<string> QdrantEndpointOption { get; } = new("--qdrant")
    {
        Description = "Qdrant endpoint for skills",
        DefaultValueFactory = _ => DefaultEndpoints.QdrantGrpc
    };

    public System.CommandLine.Option<bool> VoiceOnlyOption { get; } = new("--voice-only")
    {
        Description = "Voice-only mode (no text output)",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<bool> LocalTtsOption { get; } = new("--local-tts")
    {
        Description = "Prefer local TTS (Windows SAPI) over cloud",
        DefaultValueFactory = _ => true
    };

    public System.CommandLine.Option<bool> VoiceLoopOption { get; } = new("--voice-loop")
    {
        Description = "Continue voice conversation after command",
        DefaultValueFactory = _ => false
    };
}
