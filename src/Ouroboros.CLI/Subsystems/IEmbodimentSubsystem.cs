using Ouroboros.Application.Services;
using Ouroboros.Core.EmbodiedInteraction;

namespace Ouroboros.CLI.Subsystems;

/// <summary>
/// Manages physical embodiment: sensors, actuators, presence detection, IoT devices,
/// avatar, perception tools, and vision service.
/// </summary>
public interface IEmbodimentSubsystem : IAgentSubsystem
{
    EmbodimentController? Controller { get; }
    VirtualSelf? VirtualSelf { get; }
    BodySchema? BodySchema { get; }
    Ouroboros.Providers.Tapo.ITapoRtspClientFactory? TapoRtspFactory { get; }
    Ouroboros.Providers.Tapo.TapoRestClient? TapoRestClient { get; }
    PresenceDetector? PresenceDetector { get; }
    AgiWarmup? AgiWarmup { get; }
    bool UserWasPresent { get; set; }
    DateTime LastGreetingTime { get; set; }

    // Interactive avatar
    Application.Avatar.InteractiveAvatarService? AvatarService { get; }

    // Vision
    VisionService? VisionService { get; }
}