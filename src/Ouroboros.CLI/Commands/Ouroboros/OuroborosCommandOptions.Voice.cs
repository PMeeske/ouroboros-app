using System.CommandLine;

namespace Ouroboros.CLI.Commands.Options;

/// <summary>
/// Voice and Interaction options for the ouroboros agent command.
/// </summary>
public partial class OuroborosCommandOptions
{
    // ═══════════════════════════════════════════════════════════════════════════
    // VOICE & INTERACTION
    // ═══════════════════════════════════════════════════════════════════════════

    public Option<bool> VoiceOption { get; } = new("--voice", "-v")
    {
        Description = "Enable voice mode (speak & listen)",
        DefaultValueFactory = _ => false
    };

    public Option<bool> TextOnlyOption { get; } = new("--text-only")
    {
        Description = "Disable voice, use text input/output only",
        DefaultValueFactory = _ => false
    };

    public Option<bool> VoiceOnlyOption { get; } = new("--voice-only")
    {
        Description = "Voice-only mode (no text output)",
        DefaultValueFactory = _ => false
    };

    public Option<bool> LocalTtsOption { get; } = new("--local-tts")
    {
        Description = "Prefer local TTS (Windows SAPI) over Azure",
        DefaultValueFactory = _ => false
    };

    public Option<bool> AzureTtsOption { get; } = new("--azure-tts")
    {
        Description = "Use Azure Text-to-Speech (default when available)",
        DefaultValueFactory = _ => true
    };

    public Option<string?> AzureSpeechKeyOption { get; } = new("--azure-speech-key")
    {
        Description = "Azure Speech API key (or set AZURE_SPEECH_KEY env var)"
    };

    public Option<string> AzureSpeechRegionOption { get; } = new("--azure-speech-region")
    {
        Description = "Azure Speech region",
        DefaultValueFactory = _ => "eastus"
    };

    public Option<string> TtsVoiceOption { get; } = new("--tts-voice")
    {
        Description = "Azure TTS voice name",
        DefaultValueFactory = _ => "en-US-AvaMultilingualNeural"
    };

    public Option<bool> VoiceChannelOption { get; } = new("--voice-channel")
    {
        Description = "Enable parallel voice side channel for persona-specific audio",
        DefaultValueFactory = _ => false
    };

    public Option<bool> ListenOption { get; } = new("--listen")
    {
        Description = "Enable voice input (speech-to-text) on startup",
        DefaultValueFactory = _ => false
    };

    public Option<string?> WakeWordOption { get; } = new("--wake-word")
    {
        Description = "Wake word phrase to activate listening (default: 'Hey Iaret')",
        DefaultValueFactory = _ => "Hey Iaret"
    };

    public Option<bool> NoWakeWordOption { get; } = new("--no-wake-word")
    {
        Description = "Always-on listening without wake word",
        DefaultValueFactory = _ => false
    };

    public Option<string> SttBackendOption { get; } = new("--stt-backend")
    {
        Description = "STT backend: azure, whisper, or auto (default: auto = Azure if key present, else Whisper)",
        DefaultValueFactory = _ => "auto"
    };

    public Option<bool> VoiceLoopOption { get; } = new("--voice-loop")
    {
        Description = "Continue voice conversation in loop",
        DefaultValueFactory = _ => true
    };

    public Option<string> PersonaOption { get; } = new("--persona")
    {
        Description = "Persona: Iaret, Aria, Echo, Sage, Atlas",
        DefaultValueFactory = _ => "Iaret"
    };
}
