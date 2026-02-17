using Spectre.Console;

namespace Ouroboros.CLI.Infrastructure;

/// <summary>
/// Service wrapper for Spectre.Console functionality
/// </summary>
public interface ISpectreConsoleService
{
    /// <summary>
    /// Gets the Spectre.Console IAnsiConsole instance
    /// </summary>
    IAnsiConsole Console { get; }
    
    /// <summary>
    /// Writes a line with markup support
    /// </summary>
    void MarkupLine(string markup);
    
    /// <summary>
    /// Writes a line
    /// </summary>
    void WriteLine(string text);
    
    /// <summary>
    /// Creates a status context
    /// </summary>
    Status Status();
    
    /// <summary>
    /// Writes a table
    /// </summary>
    void Write(Table table);
}