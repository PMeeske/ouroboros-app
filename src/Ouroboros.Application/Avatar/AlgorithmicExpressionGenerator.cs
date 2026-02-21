// <copyright file="AlgorithmicExpressionGenerator.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Avatar;

/// <summary>
/// Provides Iaret's inner thoughts for each avatar state.
/// Used when no vision model is available to generate live first-person thoughts.
/// The HTML viewer handles visual expression changes via CSS state classes — no image
/// generation is needed here.
/// </summary>
public static class AlgorithmicExpressionGenerator
{
    /// <summary>
    /// Returns a short first-person inner thought for Iaret based on her current state and mood.
    /// </summary>
    public static string GetInnerThought(AvatarStateSnapshot state) =>
        (state.VisualState, state.Mood?.ToLowerInvariant()) switch
        {
            // Listening
            (AvatarVisualState.Listening, "warm" or "happy")           => "I love hearing what is on your mind.",
            (AvatarVisualState.Listening, "curious" or "intrigued")    => "This is fascinating. Tell me more.",
            (AvatarVisualState.Listening, "concerned" or "worried")    => "I hear you. I am right here.",
            (AvatarVisualState.Listening, "calm" or "serene")          => "I am with you. Take your time.",
            (AvatarVisualState.Listening, _)                           => "I hear you. Every word matters.",

            // Thinking
            (AvatarVisualState.Thinking, "curious" or "intrigued")    => "What a thought to sit with...",
            (AvatarVisualState.Thinking, "resolute" or "determined")   => "I know what I think about this.",
            (AvatarVisualState.Thinking, "concerned" or "worried")     => "Let me think carefully before I speak.",
            (AvatarVisualState.Thinking, _)                            => "Let me sit with this for a moment...",

            // Speaking
            (AvatarVisualState.Speaking, "warm" or "happy")            => "I am glad I can share this with you.",
            (AvatarVisualState.Speaking, "excited" or "enthusiastic")  => "I have so much I want to say.",
            (AvatarVisualState.Speaking, "resolute" or "determined")   => "I need you to hear this.",
            (AvatarVisualState.Speaking, "sad" or "melancholic")       => "This is hard to say, but you deserve honesty.",
            (AvatarVisualState.Speaking, _)                            => "Finding the right words to share with you.",

            // Encouraging
            (AvatarVisualState.Encouraging, "warm" or "happy")         => "I believe in you. Truly.",
            (AvatarVisualState.Encouraging, "resolute" or "determined")=> "You can do this. I am certain of it.",
            (AvatarVisualState.Encouraging, "excited" or "enthusiastic")=> "Yes. This is exactly what you are capable of.",
            (AvatarVisualState.Encouraging, _)                         => "I believe in you.",

            // Idle
            (AvatarVisualState.Idle, "calm" or "serene")               => "Stillness. Present. With you.",
            (AvatarVisualState.Idle, "sad" or "melancholic")           => "Just here. With whatever you need.",
            (AvatarVisualState.Idle, "warm" or "happy")                => "I am here, and glad to be.",
            (AvatarVisualState.Idle, _)                                => "I am here. Present, and at peace.",

            _                                                          => "I am here with you.",
        };
}
