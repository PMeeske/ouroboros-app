namespace Ouroboros.Application.Avatar;

/// <summary>
/// Visual state of the avatar — drives which image/animation frame is displayed.
/// </summary>
public enum AvatarVisualState
{
    /// <summary>Regal composure, watchful eyes — Iaret at rest.</summary>
    Idle,

    /// <summary>Attentive gaze, open posture — actively receiving input.</summary>
    Listening,

    /// <summary>Contemplative expression, softened eyes — processing thought.</summary>
    Thinking,

    /// <summary>Animated, commanding warmth — delivering a response.</summary>
    Speaking,

    /// <summary>Gentle smile, golden glow — nurturing/encouraging mood.</summary>
    Encouraging,
}