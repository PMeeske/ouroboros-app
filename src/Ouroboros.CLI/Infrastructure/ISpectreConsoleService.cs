using Spectre.Console;
using Spectre.Console.Rendering;

namespace Ouroboros.CLI.Infrastructure;

/// <summary>
/// Service wrapper for Spectre.Console functionality.
/// Use this instead of raw IAnsiConsole or Console.WriteLine in handlers and services.
/// </summary>
public interface ISpectreConsoleService
{
    /// <summary>
    /// Gets the underlying Spectre.Console IAnsiConsole instance.
    /// Prefer using the service methods below. Use this only when Spectre widgets
    /// require direct IAnsiConsole access that this interface doesn't expose.
    /// </summary>
    IAnsiConsole Console { get; }

    /// <summary>Writes a line with markup support.</summary>
    void MarkupLine(string markup);

    /// <summary>Writes a plain-text line.</summary>
    void WriteLine(string text);

    /// <summary>Writes an empty line.</summary>
    void WriteLine();

    /// <summary>Creates a Spectre.Console status context.</summary>
    Status Status();

    /// <summary>Writes a table to the console.</summary>
    void Write(Table table);

    /// <summary>
    /// Writes any IRenderable (Panel, BarChart, Tree, FigletText, Rule, etc.)
    /// to the console. This eliminates the need to unwrap to raw IAnsiConsole
    /// for rich Spectre widget rendering.
    /// </summary>
    void Write(IRenderable renderable);
}
