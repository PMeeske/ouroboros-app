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