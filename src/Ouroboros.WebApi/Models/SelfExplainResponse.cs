namespace Ouroboros.WebApi.Models;

/// <summary>
/// Response for self-explanation.
/// </summary>
public sealed record SelfExplainResponse
{
    /// <summary>
    /// Gets or sets the narrative explanation.
    /// </summary>
    public required string Narrative { get; set; }

    /// <summary>
    /// Gets or sets the execution DAG summary.
    /// </summary>
    public required string DagSummary { get; set; }

    /// <summary>
    /// Gets or sets the key events.
    /// </summary>
    public List<string>? KeyEvents { get; set; }
}