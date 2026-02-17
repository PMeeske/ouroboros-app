using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Services;

namespace Ouroboros.CLI.Subsystems;

/// <summary>
/// Manages all voice-related capabilities: TTS, STT, voice channels, and listening services.
/// </summary>
public interface IVoiceSubsystem : IAgentSubsystem
{
    VoiceModeService Service { get; }
    VoiceModeServiceV2? V2 { get; }
    VoiceSideChannel? SideChannel { get; }
    EnhancedListeningService? Listener { get; }
    bool IsListening { get; }
}