using Spectre.Console;

namespace Ouroboros.CLI.Infrastructure;

/// <summary>
/// Implementation of Spectre.Console service
/// </summary>
public class SpectreConsoleService : ISpectreConsoleService
{
    public IAnsiConsole Console => AnsiConsole.Console;
    
    public void MarkupLine(string markup)
    {
        AnsiConsole.MarkupLine(markup);
    }
    
    public void WriteLine(string text)
    {
        AnsiConsole.WriteLine(text);
    }
    
    public Status Status()
    {
        return AnsiConsole.Status();
    }
    
    public void Write(Table table)
    {
        AnsiConsole.Write(table);
    }
}