using Ouroboros.Domain.Voice;
using Ouroboros.Providers.TextToSpeech;

namespace Ouroboros.Application.Voice;

/// <summary>
/// Extension methods for LlmToVoiceBridge.
/// </summary>
public static class LlmToVoiceBridgeExtensions
{
    /// <summary>
    /// Creates a bridge from the given components.
    /// </summary>
    public static LlmToVoiceBridge CreateBridge(
        this InteractionStream stream,
        IStreamingChatModel llm,
        IStreamingTtsService tts,
        AgentPresenceController presence,
        TextToSpeechOptions? options = null)
    {
        return new LlmToVoiceBridge(stream, llm, tts, presence, options);
    }
}