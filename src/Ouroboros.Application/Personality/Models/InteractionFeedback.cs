namespace Ouroboros.Application.Personality;

/// <summary>
/// Feedback from an interaction used to evolve personality.
/// </summary>
public sealed record InteractionFeedback(
    double EngagementLevel,        // 0-1: how engaged user seemed
    double ResponseRelevance,      // 0-1: how relevant the response was
    double QuestionQuality,        // 0-1: if a question was asked, how good was it
    double ConversationContinuity, // 0-1: did conversation continue naturally
    string? TopicDiscussed,
    string? QuestionAsked,
    bool UserAskedFollowUp);