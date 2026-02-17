namespace Ouroboros.Application.Services;

/// <summary>
/// Result of screenshot element detection.
/// </summary>
public record ScreenshotDetectionResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? RawDescription { get; init; }
    public List<DetectedElement> Elements { get; init; } = new();
}