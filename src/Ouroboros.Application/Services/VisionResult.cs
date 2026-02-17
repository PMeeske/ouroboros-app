namespace Ouroboros.Application.Services;

/// <summary>
/// Result of vision analysis.
/// </summary>
public record VisionResult
{
    public bool Success { get; init; }
    public string? Description { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime Timestamp { get; init; }
    public string? AnalysisType { get; init; }
    public string? Model { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }

    public static VisionResult Failure(string message) => new()
    {
        Success = false,
        ErrorMessage = message,
        Timestamp = DateTime.Now,
    };
}