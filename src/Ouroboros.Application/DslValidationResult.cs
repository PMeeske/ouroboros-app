namespace Ouroboros.Application;

/// <summary>
/// Result of DSL validation with errors and suggested fixes.
/// </summary>
public class DslValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
    public List<string> Suggestions { get; set; } = new List<string>();
    public string? FixedDsl { get; set; }
}