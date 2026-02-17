using CommandLine;

namespace Ouroboros.CLI.Options;

/// <summary>
/// Options for listing distinctions.
/// </summary>
[Verb("distinction-list", HelpText = "List all learned distinctions with optional filtering")]
public sealed class DistinctionListOptions
{
    /// <summary>
    /// Gets or sets the stage filter.
    /// </summary>
    [Option('s', "stage", Required = false, HelpText = "Filter by learning stage (e.g., Recognition, Transition)")]
    public string? Stage { get; set; }

    /// <summary>
    /// Gets or sets the minimum fitness threshold.
    /// </summary>
    [Option('f', "min-fitness", Required = false, HelpText = "Minimum fitness threshold (0.0 - 1.0)")]
    public double? MinFitness { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of results.
    /// </summary>
    [Option('l', "limit", Required = false, Default = 50, HelpText = "Maximum number of distinctions to display")]
    public int Limit { get; set; }

    /// <summary>
    /// Gets or sets whether to show dissolved distinctions.
    /// </summary>
    [Option("show-dissolved", Required = false, Default = false, HelpText = "Include dissolved distinctions in results")]
    public bool ShowDissolved { get; set; }
}