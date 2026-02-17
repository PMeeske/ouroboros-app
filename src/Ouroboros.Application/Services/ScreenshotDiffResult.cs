namespace Ouroboros.Application.Services;

/// <summary>
/// Result of comparing two screenshots.
/// </summary>
public record ScreenshotDiffResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? BeforeDescription { get; init; }
    public string? AfterDescription { get; init; }
    public string? Changes { get; init; }
    public DateTime Timestamp { get; init; }
}