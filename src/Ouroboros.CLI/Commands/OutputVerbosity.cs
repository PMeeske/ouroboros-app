namespace Ouroboros.CLI.Commands;

/// <summary>
/// Controls how much output the CLI produces.
/// </summary>
public enum OutputVerbosity
{
    /// <summary>Responses only, no system chrome.</summary>
    Quiet,
    /// <summary>Collapsed init, no debug, clean prompts.</summary>
    Normal,
    /// <summary>Full init output, debug messages, intention bus.</summary>
    Verbose
}