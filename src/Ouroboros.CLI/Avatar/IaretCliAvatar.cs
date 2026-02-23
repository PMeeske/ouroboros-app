// <copyright file="IaretCliAvatar.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.CLI.Avatar;

/// <summary>
/// ASCII art expressions for Iaret's CLI presence.
/// Renders compact face expressions using Unicode characters,
/// matching her violet/gold avatar aesthetic.
/// </summary>
public static class IaretCliAvatar
{
    private static readonly Random Rng = new();

    /// <summary>
    /// Iaret's emotional expressions for the CLI.
    /// </summary>
    public enum Expression
    {
        Idle,        // Regal, composed, watchful
        Wink,        // Playful wink (right eye)
        Thinking,    // Contemplative, softened gaze
        Speaking,    // Animated, open mouth
        Happy,       // Encouraging smile, soft eyes
        Listening,   // Attentive, wide eyes
        Concerned,   // Worried about errors
        Playful,     // Mischievous half-smile
    }

    /// <summary>
    /// Gets a single-line inline face for embedding in messages.
    /// Example: ☥(◉ ‿) — a winking Iaret.
    /// </summary>
    public static string Inline(Expression expr) => expr switch
    {
        Expression.Idle      => "☥(◉ ◉)",
        Expression.Wink      => "☥(◉ ‿)",
        Expression.Thinking  => "☥(● ●)",
        Expression.Speaking  => "☥(◉ ◉)°",
        Expression.Happy     => "☥(◡ ◡)",
        Expression.Listening => "☥(◎ ◎)",
        Expression.Concerned => "☥(◉ ◉)~",
        Expression.Playful   => "☥(◉ ▿)",
        _ => "☥(◉ ◉)"
    };

    /// <summary>
    /// Gets a compact 3-line avatar face for section headers.
    /// </summary>
    public static string[] Standard(Expression expr)
    {
        var (eyes, mouth) = GetParts(expr);
        return
        [
            " ╭─☥─╮",
            $" │{eyes}│",
            $" ╰─{mouth}─╯"
        ];
    }

    /// <summary>
    /// Gets a 5-line uraeus (sacred cobra) face for the welcome banner.
    /// </summary>
    public static string[] Banner(Expression expr)
    {
        var (eyes, mouth) = GetParts(expr);

        // Pad eyes/mouth for the wider banner format
        string wideEyes = eyes.Length < 5 ? $" {eyes} " : eyes;
        string wideMouth = mouth.Length < 3 ? $" {mouth} " : mouth;

        return
        [
            "      ╱▲╲      ",
            "    ╱  ☥  ╲    ",
            $"   ({wideEyes})   ",
            $"    ╲{wideMouth}╱    ",
            "     ╰═══╯     "
        ];
    }

    /// <summary>
    /// Returns a contextual expression based on the current activity,
    /// with a random chance of spontaneous wink or playful look.
    /// </summary>
    public static Expression ForContext(string context)
    {
        // ~12% chance of spontaneous expression
        int roll = Rng.Next(100);
        if (roll < 7) return Expression.Wink;
        if (roll < 12) return Expression.Playful;

        return context.ToLowerInvariant() switch
        {
            "error" or "fail" or "failure" or "warning" => Expression.Concerned,
            "success" or "done" or "complete" or "passed" => Expression.Happy,
            "thinking" or "processing" or "loading" or "building" => Expression.Thinking,
            "speaking" or "response" or "reply" or "output" => Expression.Speaking,
            "listening" or "input" or "waiting" or "ready" => Expression.Listening,
            "welcome" or "hello" or "greeting" or "wake" => RandomWelcome(),
            _ => Expression.Idle
        };
    }

    /// <summary>
    /// Gets a random expression (biased toward idle with occasional winks).
    /// </summary>
    public static Expression Random()
    {
        return Rng.Next(100) switch
        {
            < 50 => Expression.Idle,
            < 65 => Expression.Happy,
            < 78 => Expression.Wink,
            < 88 => Expression.Listening,
            < 95 => Expression.Playful,
            _ => Expression.Thinking
        };
    }

    private static Expression RandomWelcome()
    {
        return Rng.Next(4) switch
        {
            0 => Expression.Happy,
            1 => Expression.Wink,
            2 => Expression.Playful,
            _ => Expression.Idle
        };
    }

    private static (string Eyes, string Mouth) GetParts(Expression expr)
    {
        return expr switch
        {
            Expression.Idle      => ("◉ ◉", "◡"),
            Expression.Wink      => ("◉ ‿", "◡"),
            Expression.Thinking  => ("● ●", "═"),
            Expression.Speaking  => ("◉ ◉", "○"),
            Expression.Happy     => ("◡ ◡", "▽"),
            Expression.Listening => ("◎ ◎", "◡"),
            Expression.Concerned => ("◉ ◉", "△"),
            Expression.Playful   => ("◉ ▿", "◡"),
            _ => ("◉ ◉", "◡")
        };
    }
}
