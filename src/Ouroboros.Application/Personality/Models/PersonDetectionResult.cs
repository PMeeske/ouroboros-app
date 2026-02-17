namespace Ouroboros.Application.Personality;

/// <summary>
/// Result of person detection attempt.
/// </summary>
public sealed record PersonDetectionResult(
    DetectedPerson Person,
    bool IsNewPerson,
    bool NameWasProvided,
    double MatchConfidence,
    string? MatchReason);