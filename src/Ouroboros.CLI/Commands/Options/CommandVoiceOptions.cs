using System.CommandLine;

namespace Ouroboros.CLI.Commands.Options;

/// <summary>
/// Voice interaction options (for commands that support voice alongside
/// the global --voice flag).
/// </summary>
public sealed class CommandVoiceOptions : IComposableOptions
{
    public Option<bool> VoiceOnlyOption { get; } = new("--voice-only")
    {
        Description = "Voice-only mode (no text output)",
        DefaultValueFactory = _ => false
    };

    public Option<bool> LocalTtsOption { get; } = new("--local-tts")
    {
        Description = "Prefer local TTS (Windows SAPI) over cloud",
        DefaultValueFactory = _ => true
    };

    public Option<bool> VoiceLoopOption { get; } = new("--voice-loop")
    {
        Description = "Continue voice conversation after command",
        DefaultValueFactory = _ => false
    };

    public void AddToCommand(Command command)
    {
        command.Add(VoiceOnlyOption);
        command.Add(LocalTtsOption);
        command.Add(VoiceLoopOption);
    }
}