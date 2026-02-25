namespace Ouroboros.Application.Personality;

/// <summary>
/// Curiosity driver - determines what questions to proactively ask.
/// </summary>
public sealed record CuriosityDriver(
    string Topic,
    double Interest,         // 0.0-1.0 how interested
    string[] RelatedQuestions,
    DateTime LastAsked,
    int AskCount)
{
    /// <summary>Determines if enough time has passed to ask again.</summary>
    public bool CanAskAgain(TimeSpan cooldown) =>
        DateTime.UtcNow - LastAsked > cooldown;
}