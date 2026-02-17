namespace Ouroboros.Application.Services;

/// <summary>
/// Result of text extraction from a screenshot.
/// </summary>
public record TextExtractionResult
{
    public bool Success { get; init; }
    public string ExtractedText { get; init; } = "";
    public string? ErrorMessage { get; init; }
    public DateTime Timestamp { get; init; }
}