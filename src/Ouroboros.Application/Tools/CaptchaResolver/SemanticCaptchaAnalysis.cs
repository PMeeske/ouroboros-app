namespace Ouroboros.Application.Tools.CaptchaResolver;

/// <summary>
/// Semantic analysis result for CAPTCHA content.
/// </summary>
public record SemanticCaptchaAnalysis(
    bool IsCaptcha,
    string CaptchaType,
    string? ChallengeDescription,
    string? SuggestedApproach,
    double Confidence);