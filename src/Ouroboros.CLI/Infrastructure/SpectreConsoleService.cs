using Spectre.Console;
using Spectre.Console.Rendering;

namespace Ouroboros.CLI.Infrastructure;

/// <summary>
/// Implementation of Spectre.Console service.
/// </summary>
public class SpectreConsoleService : ISpectreConsoleService
{
    public IAnsiConsole Console => AnsiConsole.Console;

    public void MarkupLine(string markup)
        => AnsiConsole.MarkupLine(markup);

    public void WriteLine(string text)
        => AnsiConsole.WriteLine(text);

    public void WriteLine()
        => AnsiConsole.WriteLine();

    public Status Status()
        => AnsiConsole.Status();

    public void Write(Table table)
        => AnsiConsole.Write(table);

    public void Write(IRenderable renderable)
        => AnsiConsole.Write(renderable);
}
