using Ouroboros.Core.Hyperon;

namespace Ouroboros.Application.Hyperon;

/// <summary>
/// Internal pattern subscription record.
/// </summary>
internal sealed record PatternSubscription(
    string Id,
    Atom Pattern,
    Action<PatternMatchEvent> Callback);