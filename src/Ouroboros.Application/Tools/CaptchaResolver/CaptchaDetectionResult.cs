namespace Ouroboros.Application.Tools.CaptchaResolver;

/// <summary>
/// Represents the result of a CAPTCHA detection check.
/// </summary>
public record CaptchaDetectionResult(
    bool IsCaptcha,
    string CaptchaType,
    string? ChallengeText = null);