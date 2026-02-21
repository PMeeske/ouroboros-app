// <copyright file="AvatarState.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Avatar;

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
    /// <summary>Conversational topic hint for stage positioning and facial expression (e.g. "technical", "emotional").</summary>
    public string? Topic { get; init; }

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