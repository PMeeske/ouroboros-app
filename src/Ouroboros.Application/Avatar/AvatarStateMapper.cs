namespace Ouroboros.Application.Avatar;

/// <summary>
/// Maps presence and mood information to an <see cref="AvatarVisualState"/>.
/// </summary>
public static class AvatarStateMapper
{
    private static readonly HashSet<string> WarmMoods = new(StringComparer.OrdinalIgnoreCase)
    {
        "warm", "supportive", "nurturing", "encouraging", "maternal",
        "gentle", "caring", "content", "satisfied", "cheerful",
        "happy", "joyful", "grateful", "loving", "affectionate",
        "proud", "hopeful", "serene", "peaceful",
    };

    private static readonly HashSet<string> IntenseMoods = new(StringComparer.OrdinalIgnoreCase)
    {
        "excited", "enthusiastic", "passionate", "energetic", "eager",
        "amazed", "astonished", "thrilled", "inspired", "determined",
    };

    private static readonly HashSet<string> ThoughtfulMoods = new(StringComparer.OrdinalIgnoreCase)
    {
        "curious", "intrigued", "contemplative", "reflective", "pensive",
        "analytical", "focused", "thoughtful", "philosophical", "wondering",
        "melancholic", "wistful", "nostalgic",
    };

    private static readonly HashSet<string> AttentiveMoods = new(StringComparer.OrdinalIgnoreCase)
    {
        "concerned", "worried", "empathetic", "sympathetic", "compassionate",
        "protective", "vigilant", "alert", "cautious",
    };

    /// <summary>
    /// Resolves the visual state from presence state name and current mood.
    /// </summary>
    public static AvatarVisualState Resolve(string presenceState, string mood)
    {
        // Warm/nurturing moods → Encouraging during idle or speaking
        if (WarmMoods.Contains(mood))
        {
            return presenceState switch
            {
                "Idle" or "Paused" => AvatarVisualState.Encouraging,
                "Speaking" => AvatarVisualState.Encouraging,
                "Listening" => AvatarVisualState.Encouraging,
                _ => ResolveFromPresence(presenceState),
            };
        }

        // Intense/excited moods → Speaking (animated, energetic) during idle
        if (IntenseMoods.Contains(mood))
        {
            return presenceState switch
            {
                "Idle" or "Paused" => AvatarVisualState.Speaking,
                "Listening" => AvatarVisualState.Listening,
                _ => ResolveFromPresence(presenceState),
            };
        }

        // Thoughtful/curious moods → Thinking during idle or listening
        if (ThoughtfulMoods.Contains(mood))
        {
            return presenceState switch
            {
                "Idle" or "Paused" => AvatarVisualState.Thinking,
                "Listening" => AvatarVisualState.Thinking,
                _ => ResolveFromPresence(presenceState),
            };
        }

        // Concerned/attentive moods → Listening (attentive, wide-eyed)
        if (AttentiveMoods.Contains(mood))
        {
            return presenceState switch
            {
                "Idle" or "Paused" => AvatarVisualState.Listening,
                "Speaking" => AvatarVisualState.Speaking,
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
