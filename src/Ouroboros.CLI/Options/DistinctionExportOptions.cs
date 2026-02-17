using CommandLine;

namespace Ouroboros.CLI.Options;

/// <summary>
/// Options for exporting distinctions.
/// </summary>
[Verb("distinction-export", HelpText = "Export distinctions to JSON format")]
public sealed class DistinctionExportOptions
{
    /// <summary>
    /// Gets or sets the output file path.
    /// </summary>
    [Option('o', "output", Required = false, HelpText = "Output file path (default: distinctions-export.json)")]
    public string? Output { get; set; }

    /// <summary>
    /// Gets or sets whether to include dissolved distinctions.
    /// </summary>
    [Option("include-dissolved", Required = false, Default = false, HelpText = "Include dissolved distinctions in export")]
    public bool IncludeDissolved { get; set; }
}