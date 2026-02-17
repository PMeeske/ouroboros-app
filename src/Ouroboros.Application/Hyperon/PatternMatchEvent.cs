using Ouroboros.Core.Hyperon;

namespace Ouroboros.Application.Hyperon;

/// <summary>
/// Event raised when a pattern subscription matches.
/// </summary>
public sealed record PatternMatchEvent(
    string SubscriptionId,
    Atom Pattern,
    Atom MatchedAtom,
    Substitution Bindings,
    DateTime Timestamp);