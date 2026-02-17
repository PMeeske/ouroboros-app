using CommandLine;

namespace Ouroboros.CLI.Options;

/// <summary>
/// Options for clearing all distinctions.
/// </summary>
[Verb("distinction-clear", HelpText = "Clear all distinctions (WARNING: destructive operation)")]
public sealed class DistinctionClearOptions
{
    /// <summary>
    /// Gets or sets whether confirmation is provided.
    /// </summary>
    [Option('y', "confirm", Required = false, Default = false, HelpText = "Skip confirmation prompt")]
    public bool Confirm { get; set; }
}