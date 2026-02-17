using CommandLine;

namespace Ouroboros.CLI.Options;

/// <summary>
/// Options for showing distinction learning status.
/// </summary>
[Verb("distinction-status", HelpText = "Show distinction learning status and statistics")]
public sealed class DistinctionStatusOptions
{
    /// <summary>
    /// Gets or sets whether to show verbose details.
    /// </summary>
    [Option('v', "verbose", Required = false, Default = false, HelpText = "Show verbose details including storage paths")]
    public bool Verbose { get; set; }
}