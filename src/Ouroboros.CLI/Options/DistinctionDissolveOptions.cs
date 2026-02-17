using CommandLine;

namespace Ouroboros.CLI.Options;

/// <summary>
/// Options for dissolving low-fitness distinctions.
/// </summary>
[Verb("distinction-dissolve", HelpText = "Dissolve low-fitness distinctions to free storage")]
public sealed class DistinctionDissolveOptions
{
    /// <summary>
    /// Gets or sets the fitness threshold.
    /// </summary>
    [Option('t', "threshold", Required = false, Default = 0.3, HelpText = "Fitness threshold below which to dissolve (0.0 - 1.0)")]
    public double Threshold { get; set; }

    /// <summary>
    /// Gets or sets whether this is a dry run.
    /// </summary>
    [Option('d', "dry-run", Required = false, Default = false, HelpText = "Preview what would be dissolved without actually doing it")]
    public bool DryRun { get; set; }

    /// <summary>
    /// Gets or sets the dissolution strategy.
    /// </summary>
    [Option("strategy", Required = false, Default = "fitness", HelpText = "Dissolution strategy: fitness, oldest, lru")]
    public string Strategy { get; set; } = "fitness";
}