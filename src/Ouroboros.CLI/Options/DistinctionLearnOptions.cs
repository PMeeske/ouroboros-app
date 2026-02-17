using CommandLine;

namespace Ouroboros.CLI.Options;

/// <summary>
/// Options for manual distinction learning.
/// </summary>
[Verb("distinction-learn", HelpText = "Manually trigger distinction learning on provided text")]
public sealed class DistinctionLearnOptions
{
    /// <summary>
    /// Gets or sets the text to learn from.
    /// </summary>
    [Value(0, Required = true, MetaName = "text", HelpText = "Text content to learn distinctions from")]
    public required string Text { get; set; }

    /// <summary>
    /// Gets or sets whether to show learning stages.
    /// </summary>
    [Option("show-stages", Required = false, Default = false, HelpText = "Display the learning process through consciousness stages")]
    public bool ShowStages { get; set; }

    /// <summary>
    /// Gets or sets the learning stage.
    /// </summary>
    [Option('s', "stage", Required = false, Default = "Recognition", HelpText = "Consciousness stage for learning (Recognition, Transition, etc.)")]
    public string Stage { get; set; } = "Recognition";
}