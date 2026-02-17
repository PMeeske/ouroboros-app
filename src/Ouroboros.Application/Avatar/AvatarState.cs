// <copyright file="AvatarState.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

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

/// <summary>
/// Complete avatar state snapshot broadcast to renderers.
/// </summary>
public sealed record AvatarStateSnapshot(
    AvatarVisualState VisualState,
    string Mood,
    double Energy,
    double Positivity,
    string? StatusText,
    string PersonaName,
    DateTime Timestamp)
{
    /// <summary>Creates a default idle snapshot.</summary>
    public static AvatarStateSnapshot Default(string persona = "Iaret") => new(
        AvatarVisualState.Idle,
        "neutral",
        0.5,
        0.5,
        null,
        persona,
        DateTime.UtcNow);
}

/// <summary>
/// Maps presence and mood information to an <see cref="AvatarVisualState"/>.
/// </summary>
public static class AvatarStateMapper
{
    private static readonly HashSet<string> WarmMoods = new(StringComparer.OrdinalIgnoreCase)
    {
        "warm", "supportive", "nurturing", "encouraging", "maternal",
        "gentle", "caring", "content", "satisfied", "cheerful",
    };

    /// <summary>
    /// Resolves the visual state from presence state name and current mood.
    /// </summary>
    /// <param name="presenceState">Agent presence state name (Idle, Listening, Processing, Speaking, etc.).</param>
    /// <param name="mood">Current mood name.</param>
    /// <returns>The avatar visual state to display.</returns>
    public static AvatarVisualState Resolve(string presenceState, string mood)
    {
        // Warm/nurturing moods override during idle or speaking
        if (WarmMoods.Contains(mood))
        {
            return presenceState switch
            {
                "Idle" or "Paused" => AvatarVisualState.Encouraging,
                "Speaking" => AvatarVisualState.Encouraging,
                _ => ResolveFromPresence(presenceState),
            };
        }

        return ResolveFromPresence(presenceState);
    }

    private static AvatarVisualState ResolveFromPresence(string presenceState)
    {
        return presenceState switch
        {
            "Listening" => AvatarVisualState.Listening,
            "Processing" => AvatarVisualState.Thinking,
            "Speaking" => AvatarVisualState.Speaking,
            "Interrupted" => AvatarVisualState.Listening,
            "Paused" => AvatarVisualState.Idle,
            _ => AvatarVisualState.Idle,
        };
    }
}
