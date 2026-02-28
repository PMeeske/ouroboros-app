using System.CommandLine;

namespace Ouroboros.CLI.Infrastructure;

/// <summary>
/// Wraps a <see cref="RootCommand"/> so that services (e.g. voice integration)
/// can re-invoke the CLI pipeline with new arguments without a direct
/// dependency on the System.CommandLine tree built in <c>Program.cs</c>.
/// <para>
/// Registered as a singleton in DI during host construction. The
/// <see cref="RootCommand"/> is set from <c>Program.cs</c> once the
/// full command tree has been assembled (deferred initialisation).
/// </para>
/// </summary>
public sealed class CommandLineInvoker : ICommandLineInvoker
{
    private RootCommand? _rootCommand;

    /// <summary>
    /// Sets the root command after the full command tree has been built.
    /// Must be called once from <c>Program.cs</c> before any voice
    /// command dispatch occurs.
    /// </summary>
    public void SetRootCommand(RootCommand rootCommand)
    {
        _rootCommand = rootCommand ?? throw new ArgumentNullException(nameof(rootCommand));
    }

    /// <inheritdoc/>
    public async Task<int> InvokeAsync(string[] args, CancellationToken cancellationToken = default)
    {
        if (_rootCommand is null)
        {
            throw new InvalidOperationException(
                "CommandLineInvoker has not been initialised. " +
                "Call SetRootCommand() from Program.cs after building the command tree.");
        }

        var parseResult = _rootCommand.Parse(args);
        return await parseResult.InvokeAsync();
    }
}
