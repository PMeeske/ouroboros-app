namespace Ouroboros.Application.Services;

/// <summary>
/// A conversation session containing multiple turns.
/// </summary>
public sealed record ConversationSession(
    string SessionId,
    string PersonaName,
    DateTime StartedAt,
    DateTime LastActivityAt,
    List<ConversationTurn> Turns,
    string? Summary = null);