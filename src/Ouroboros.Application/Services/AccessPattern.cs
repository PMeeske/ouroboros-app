namespace Ouroboros.Application.Services;

/// <summary>
/// Access pattern tracking for knowledge reorganization.
/// </summary>
public sealed record AccessPattern
{
    public string PointId { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public int AccessCount { get; init; }
    public DateTime LastAccessed { get; init; }
    public List<string> CoAccessedWith { get; init; } = new();
}