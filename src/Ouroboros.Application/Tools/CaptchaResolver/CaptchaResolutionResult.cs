namespace Ouroboros.Application.Tools.CaptchaResolver;

/// <summary>
/// Represents the result of a CAPTCHA resolution attempt.
/// </summary>
public record CaptchaResolutionResult(
    bool Success,
    string? ResolvedContent = null,
    string? ErrorMessage = null);