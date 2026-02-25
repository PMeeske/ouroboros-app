namespace Ouroboros.Application.Services;

/// <summary>
/// Result of validating screen state against expectations.
/// </summary>
public record ValidationResult
{
    public bool Success { get; init; }
    public bool AllPassed { get; init; }
    public string Details { get; init; } = "";
    public string[] Expectations { get; init; } = Array.Empty<string>();
    public DateTime Timestamp { get; init; }
}