namespace Ouroboros.Application.Services;

/// <summary>
/// Result of action suggestion based on screenshot analysis.
/// </summary>
public record ActionSuggestionResult
{
    public bool Success { get; init; }
    public string Suggestion { get; init; } = "";
    public string? ErrorMessage { get; init; }
    public string? Goal { get; init; }
    public DateTime Timestamp { get; init; }
}