using Ouroboros.Core.Hyperon;

namespace Ouroboros.Application.Hyperon;

/// <summary>
/// Event raised when a flow completes.
/// </summary>
public sealed record FlowCompletionEvent(
    string FlowName,
    bool Success,
    IReadOnlyList<Atom> Results,
    TimeSpan Duration,
    string? Error = null);