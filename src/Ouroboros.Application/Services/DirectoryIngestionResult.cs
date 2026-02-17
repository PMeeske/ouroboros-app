namespace Ouroboros.Application.Services;

public record DirectoryIngestionResult
{
    public required IReadOnlyList<string> VectorIds { get; init; }
    public required DirectoryIngestionStats Stats { get; init; }
}