namespace Ouroboros.CLI.Infrastructure;

/// <summary>
/// Consistent tool-call display inspired by Crush's tool rendering model:
/// each tool shows a header line (status icon + name + truncated param) and
/// an optional body (output lines, truncated to <see cref="DefaultBodyLines"/>).
///
/// Status icons:
///   ●  pending / running
///   ✓  succeeded
///   ✗  failed
///   ⊘  cancelled
/// </summary>
public static class ToolRenderer
{
    /// <summary>Number of output lines shown before truncation.</summary>
    public const int DefaultBodyLines = 10;

    /// <summary>Max characters of tool parameter shown on the header line.</summary>
    private const int MaxParamChars = 60;

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes the "tool started" header: <c>  ● ToolName  param</c>
    /// </summary>
    public static void WriteToolStart(IConsoleOutput output, string toolName, string? param = null)
    {
        var header = BuildHeader("●", toolName, param);
        output.WriteSystem(header);
    }

    /// <summary>
    /// Writes the completed tool header + (optionally) a truncated body.
    /// Replaces the pending ● with ✓ or ✗.
    /// </summary>
    public static void WriteToolDone(
        IConsoleOutput output,
        string toolName,
        bool success,
        string? outputText = null,
        int maxLines = DefaultBodyLines)
    {
        var icon = success ? "✓" : "✗";
        var header = BuildHeader(icon, toolName, param: null);

        if (string.IsNullOrWhiteSpace(outputText))
        {
            output.WriteSystem(header);
            return;
        }

        // Print header then indented, truncated body
        output.WriteSystem(header);
        WriteBody(outputText, maxLines);
    }

    /// <summary>
    /// Writes a cancelled tool line.
    /// </summary>
    public static void WriteToolCancelled(IConsoleOutput output, string toolName)
    {
        output.WriteSystem(BuildHeader("⊘", toolName, param: null));
    }

    // ── Raw console helpers (for callers that hold the console lock) ───────────

    /// <summary>
    /// Writes the tool header directly to <see cref="Console"/>.
    /// Caller is responsible for thread safety.
    /// </summary>
    public static void WriteHeaderRaw(string icon, string toolName, string? param)
    {
        Console.ForegroundColor = icon switch
        {
            "✓" => ConsoleColor.Green,
            "✗" => ConsoleColor.Red,
            "⊘" => ConsoleColor.DarkGray,
            _   => ConsoleColor.DarkCyan,   // ● pending
        };
        Console.Write($"  {icon} {toolName}");
        Console.ResetColor();

        if (!string.IsNullOrEmpty(param))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"  {Truncate(param, MaxParamChars)}");
            Console.ResetColor();
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Writes a body block (indented, truncated) directly to <see cref="Console"/>.
    /// Caller is responsible for thread safety.
    /// </summary>
    public static void WriteBodyRaw(string text, int maxLines = DefaultBodyLines)
    {
        WriteBody(text, maxLines);
    }

    // ── Internals ──────────────────────────────────────────────────────────────

    private static string BuildHeader(string icon, string toolName, string? param)
    {
        var p = string.IsNullOrEmpty(param) ? "" : $"  {Truncate(param, MaxParamChars)}";
        return $"{icon} {toolName}{p}";
    }

    private static void WriteBody(string text, int maxLines)
    {
        var lines = text.Split('\n');
        var shown = Math.Min(lines.Length, maxLines);

        Console.ForegroundColor = ConsoleColor.DarkGray;
        for (var i = 0; i < shown; i++)
        {
            var line = lines[i].TrimEnd();
            if (line.Length > 0)
                Console.WriteLine($"    {line}");
        }

        if (lines.Length > maxLines)
            Console.WriteLine($"    ... ({lines.Length - maxLines} more lines)");

        Console.ResetColor();
        Console.WriteLine();
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";
}
