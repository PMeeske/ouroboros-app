namespace Ouroboros.Application.Services;

/// <summary>
/// Represents an autonomous action to be executed.
/// </summary>
public record AutonomousAction
{
    public string ToolName { get; init; } = "";
    public string ToolInput { get; init; } = "";
    public string Description { get; init; } = "";
    public DateTime RequestedAt { get; init; }
    public DateTime? ExecutedAt { get; set; }
    public bool Success { get; set; }
    public string? Result { get; set; }
}