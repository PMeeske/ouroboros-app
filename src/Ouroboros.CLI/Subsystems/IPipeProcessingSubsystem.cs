// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

/// <summary>
/// Manages pipe-based command chaining and non-interactive batch/exec/pipe-mode execution.
/// </summary>
public interface IPipeProcessingSubsystem : IAgentSubsystem
{
    /// <summary>Processes input with support for | piping syntax, substituting $PIPE / $_ placeholders.</summary>
    Task<string> ProcessInputWithPipingAsync(string input, int maxPipeDepth = 5);

    /// <summary>Runs the agent in non-interactive mode (--exec, --batch, or --pipe).</summary>
    Task RunNonInteractiveModeAsync();
}
