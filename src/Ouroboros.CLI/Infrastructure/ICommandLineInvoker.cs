namespace Ouroboros.CLI.Infrastructure;

/// <summary>
/// Abstracts the ability to re-invoke a CLI command with new arguments.
/// Used by <see cref="VoiceIntegrationService"/> to dispatch commands
/// after speech recognition has replaced the original arguments.
/// </summary>
public interface ICommandLineInvoker
{
    /// <summary>
    /// Parses and invokes the CLI with the given arguments, as if they were
    /// passed on the command line. Returns the process exit code.
    /// </summary>
    /// <param name="args">The command-line arguments to invoke.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exit code produced by the command.</returns>
    Task<int> InvokeAsync(string[] args, CancellationToken cancellationToken = default);
}
