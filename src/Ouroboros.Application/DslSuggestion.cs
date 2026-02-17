namespace Ouroboros.Application;

/// <summary>
/// Represents a DSL suggestion with explanation.
/// </summary>
public class DslSuggestion
{
    public string Token { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public double Confidence { get; set; } = 1.0;
}