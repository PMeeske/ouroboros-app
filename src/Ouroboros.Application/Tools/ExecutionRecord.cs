namespace Ouroboros.Application.Tools;

/// <summary>
/// Represents a single execution record (tool, skill, or pipeline).
/// </summary>
public sealed record ExecutionRecord(
    string Id,
    string ExecutionType,
    string Name,
    string Input,
    string Output,
    bool Success,
    TimeSpan Duration,
    DateTime Timestamp,
    Dictionary<string, string> Metadata);