using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Commands;
using Spectre.Console;

namespace Ouroboros.CLI.Infrastructure;

/// <summary>
/// Centralized console output with verbosity-aware routing, init buffering,
/// and an inline spinner that uses carriage-return overwrite.
/// Uses Spectre.Console for all rendering with Iaret's purple/gold theme.
/// </summary>
public sealed class ConsoleOutput : IConsoleOutput
{
    private readonly object _lock = new();
    private readonly List<(string Name, bool Success, string? Detail)> _initRecords = [];

    public OutputVerbosity Verbosity { get; }

    public ConsoleOutput(OutputVerbosity verbosity)
    {
        Verbosity = verbosity;
    }

    // ── Init-phase output ──────────────────────────────────────

    public void RecordInit(string subsystemName, bool success, string? detail = null)
    {
        _initRecords.Add((subsystemName, success, detail));

        if (Verbosity == OutputVerbosity.Verbose)
        {
            var icon = success ? "[green]✓[/]" : "[grey]○[/]";
            var name = Markup.Escape(subsystemName);
            lock (_lock)
            {
                var line = detail != null
                    ? $"  {icon} {name}: {Markup.Escape(detail)}"
                    : $"  {icon} {name}";
                AnsiConsole.MarkupLine(line);
            }
        }
    }

    public void FlushInitSummary()
    {
        if (Verbosity == OutputVerbosity.Quiet) return;

        if (Verbosity == OutputVerbosity.Verbose)
        {
            lock (_lock)
            {
                AnsiConsole.MarkupLine($"\n  [green]✓ Ouroboros fully initialized ({_initRecords.Count} subsystems)[/]\n");
            }
            return;
        }

        // Normal mode: collapsed summary
        var active = _initRecords.Count(r => r.Success);
        var failed = _initRecords.Where(r => !r.Success).ToList();

        lock (_lock)
        {
            if (failed.Count == 0)
            {
                AnsiConsole.MarkupLine($"  [green]● Ready ({active} subsystems active)[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"  [yellow]● Ready ({active} subsystems active, {failed.Count} disabled)[/]");
            }

            foreach (var (name, _, detail) in failed)
            {
                var escaped = detail != null
                    ? $"    [grey]○ {Markup.Escape(name)}: {Markup.Escape(detail)}[/]"
                    : $"    [grey]○ {Markup.Escape(name)}[/]";
                AnsiConsole.MarkupLine(escaped);
            }

            AnsiConsole.WriteLine();
        }
    }

    // ── Conversation output ────────────────────────────────────

    public void WriteResponse(string personaName, string text)
    {
        lock (_lock)
        {
            var expr = IaretCliAvatar.ForContext("speaking");
            var face = IaretCliAvatar.Inline(expr);
            AnsiConsole.MarkupLine($"\n  {OuroborosTheme.GoldText(face)} {OuroborosTheme.Accent(personaName)}: {Markup.Escape(text)}");
        }
    }

    public void WriteSystem(string text)
    {
        if (Verbosity == OutputVerbosity.Quiet) return;

        lock (_lock)
        {
            AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(text)}[/]");
        }
    }

    // ── Debug / diagnostic output ──────────────────────────────

    public void WriteDebug(string text)
    {
        if (Verbosity < OutputVerbosity.Verbose) return;

        lock (_lock)
        {
            AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(text)}[/]");
        }
    }

    public void WriteWarning(string text)
    {
        if (Verbosity == OutputVerbosity.Quiet) return;

        lock (_lock)
        {
            var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
            AnsiConsole.MarkupLine($"  [yellow]{Markup.Escape(face)} ⚠ {Markup.Escape(text)}[/]");
        }
    }

    public void WriteError(string text)
    {
        lock (_lock)
        {
            var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} ✗ {Markup.Escape(text)}[/]");
        }
    }

    // ── Tool display (Crush-style) ─────────────────────────────

    public void WriteToolCall(string toolName, string? param = null)
    {
        if (Verbosity == OutputVerbosity.Quiet) return;

        lock (_lock)
        {
            ToolRenderer.WriteHeaderRaw("●", toolName, param);
        }
    }

    public void WriteToolResult(string toolName, bool success, string? output = null, int maxLines = 10)
    {
        if (Verbosity == OutputVerbosity.Quiet) return;

        lock (_lock)
        {
            ToolRenderer.WriteHeaderRaw(success ? "✓" : "✗", toolName, param: null);
            if (!string.IsNullOrWhiteSpace(output))
                ToolRenderer.WriteBodyRaw(output, maxLines);
        }
    }

    // ── Status bar ─────────────────────────────────────────────

    public void WriteStatusBar(string model, string? workingDir = null, int? contextPct = null)
    {
        if (Verbosity == OutputVerbosity.Quiet) return;

        lock (_lock)
        {
            var dir = workingDir != null
                ? TruncatePath(workingDir, 30)
                : null;

            var parts = new List<string> { Markup.Escape(model) };
            if (dir != null) parts.Add(Markup.Escape(dir));

            var status = string.Join(" · ", parts);

            if (contextPct.HasValue)
            {
                var color = contextPct.Value switch
                {
                    >= 80 => "red",
                    >= 50 => "yellow",
                    _ => "rgb(148,103,189)",
                };
                AnsiConsole.MarkupLine($"  [rgb(148,103,189)]{status}[/]  [{color}]{contextPct.Value}%[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"  [rgb(148,103,189)]{status}[/]");
            }
        }
    }

    private static string TruncatePath(string path, int max)
    {
        if (path.Length <= max) return path;
        var parts = path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? "…/" + parts[^1] : "…" + path[^(max - 1)..];
    }

    // ── Spinner ────────────────────────────────────────────────

    public ISpinnerHandle StartSpinner(string label)
    {
        if (Verbosity == OutputVerbosity.Quiet)
            return NullSpinnerHandle.Instance;

        return new InlineSpinner(label, _lock);
    }

    // ── Welcome / Banner ───────────────────────────────────────

    public void WriteWelcome(string personaName, string model, string? mood = null)
    {
        if (Verbosity == OutputVerbosity.Quiet) return;

        lock (_lock)
        {
            AnsiConsole.WriteLine();

            var expr = IaretCliAvatar.ForContext("welcome");
            var banner = IaretCliAvatar.Banner(expr);

            // Build banner content with purple background
            var rows = new Rows(
                banner.Select(line => new Markup($"[bold rgb(255,200,50) on rgb(60,0,120)]{Markup.Escape(line)}[/]"))
                    .Concat(
                    [
                        new Markup($"[bold rgb(255,200,50) on rgb(60,0,120)]   O U R O B O R O S   [/]"),
                        new Markup($"[rgb(148,103,189) on rgb(60,0,120)]  Unified AI Agent System  [/]"),
                    ]));

            var panel = new Panel(rows)
            {
                Border = BoxBorder.Double,
                BorderStyle = OuroborosTheme.BorderStyle,
                Padding = new Padding(2, 1),
                Header = new PanelHeader($"[bold rgb(255,200,50)] ☥ {Markup.Escape(personaName)} ☥ [/]", Justify.Center),
            };

            AnsiConsole.Write(panel);

            var moodPart = mood != null ? $" · mood: {Markup.Escape(mood)}" : "";
            AnsiConsole.MarkupLine($"  [rgb(148,103,189)]{Markup.Escape(personaName)} · {Markup.Escape(model)}{moodPart} · help | status | exit[/]");
            AnsiConsole.WriteLine();
        }
    }

    // ── Wake event ─────────────────────────────────────────────

    /// <summary>
    /// Displays Iaret's wake greeting when "hey iaret" is detected in text input.
    /// </summary>
    public void WriteWakeGreeting()
    {
        lock (_lock)
        {
            var expr = IaretCliAvatar.ForContext("wake");
            var face = IaretCliAvatar.Standard(expr);

            AnsiConsole.WriteLine();
            foreach (var line in face)
            {
                AnsiConsole.MarkupLine($"  [bold rgb(255,200,50) on rgb(60,0,120)] {Markup.Escape(line)} [/]");
            }
            AnsiConsole.MarkupLine($"  [rgb(148,103,189)]I'm here. How can I help?[/]");
            AnsiConsole.WriteLine();
        }
    }
}
