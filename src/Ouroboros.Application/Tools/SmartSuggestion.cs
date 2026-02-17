namespace Ouroboros.Application.Tools;

/// <summary>
/// Smart suggestion result combining multiple algorithms.
/// </summary>
public sealed class SmartSuggestion
{
    public string Goal { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double ConfidenceScore { get; set; }

    public List<string> MeTTaSuggestions { get; set; } = new();
    public List<PatternSummary> SimilarPatterns { get; set; } = new();
    public LLMActionSuggestion? LLMSuggestion { get; set; }
    public List<(string ActionType, string ActionName)> EvolvedSequence { get; set; } = new();
    public List<string> RelatedConcepts { get; set; } = new();
}