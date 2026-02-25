using Ouroboros.Tools.MeTTa;

namespace Ouroboros.Application.Personality;

/// <summary>Event args for Hyperon pattern matches.</summary>
public class HyperonPatternMatchEventArgs : EventArgs
{
    /// <summary>The pattern match result.</summary>
    public PatternMatch Match { get; }

    /// <summary>The pattern that was matched.</summary>
    public string Pattern => Match.Pattern;

    /// <summary>The subscription that triggered this match.</summary>
    public string SubscriptionId => Match.SubscriptionId;

    /// <summary>Variable bindings from the match.</summary>
    public IReadOnlyDictionary<string, string> Bindings { get; }

    public HyperonPatternMatchEventArgs(PatternMatch match)
    {
        Match = match;
        // Convert Substitution bindings to string dictionary
        var dict = new Dictionary<string, string>();
        foreach (var kvp in match.Bindings.Bindings)
        {
            dict[kvp.Key] = kvp.Value.ToSExpr();
        }
        Bindings = dict;
    }
}