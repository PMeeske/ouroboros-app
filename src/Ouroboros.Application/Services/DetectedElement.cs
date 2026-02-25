namespace Ouroboros.Application.Services;

/// <summary>
/// A detected UI element in a screenshot.
/// </summary>
public record DetectedElement
{
    public string Type { get; init; } = "";
    public string Label { get; init; } = "";
    public string? Position { get; init; }
    public string? State { get; init; }
    public string? RawLine { get; init; }
}