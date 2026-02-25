using System.CommandLine;

namespace Ouroboros.CLI.Commands.Options;

/// <summary>
/// Composable option groups that eliminate duplication across command option classes.
/// Each group represents a coherent set of options that appear on multiple commands.
/// Commands compose the groups they need via <see cref="IComposableOptions"/>.
/// </summary>
public interface IComposableOptions
{
    void AddToCommand(Command command);
}