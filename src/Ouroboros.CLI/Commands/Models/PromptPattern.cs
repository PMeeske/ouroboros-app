namespace Ouroboros.CLI.Commands;

/// <summary>
/// Tracks the effectiveness of prompt patterns and learns what works.
/// </summary>
public sealed class PromptPattern
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; init; } = "";
    public string Template { get; init; } = "";
    public int UsageCount { get; set; }
    public int SuccessCount { get; set; } // Tool was called when expected
    public int FailureCount { get; set; } // Tool should have been called but wasn't
    public double SuccessRate => UsageCount > 0 ? (double)SuccessCount / UsageCount : 0.5;
    public DateTime LastUsed { get; set; } = DateTime.UtcNow;
    public List<string> SuccessfulVariants { get; init; } = new();
    public List<string> FailedVariants { get; init; } = new();
}