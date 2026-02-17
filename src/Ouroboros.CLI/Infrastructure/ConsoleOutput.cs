using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Infrastructure;

/// <summary>
/// Centralized console output with verbosity-aware routing, init buffering,
/// and an inline spinner that uses carriage-return overwrite.
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
            // Show each line immediately in verbose mode
            var icon = success ? "✓" : "○";
            var color = success ? ConsoleColor.Green : ConsoleColor.DarkGray;
            lock (_lock)
            {
                Console.ForegroundColor = color;
                var line = detail != null ? $"  {icon} {subsystemName}: {detail}" : $"  {icon} {subsystemName}";
                Console.WriteLine(line);
                Console.ResetColor();
            }
        }
    }

    public void FlushInitSummary()
    {
        if (Verbosity == OutputVerbosity.Quiet) return;

        if (Verbosity == OutputVerbosity.Verbose)
        {
            // Already printed line-by-line in RecordInit
            lock (_lock)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n  ✓ Ouroboros fully initialized ({_initRecords.Count} subsystems)\n");
                Console.ResetColor();
            }
            return;
        }

        // Normal mode: collapsed summary
        var active = _initRecords.Count(r => r.Success);
        var failed = _initRecords.Where(r => !r.Success).ToList();

        lock (_lock)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            if (failed.Count == 0)
            {
                Console.WriteLine($"  ● Ready ({active} subsystems active)");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  ● Ready ({active} subsystems active, {failed.Count} disabled)");
            }
            Console.ResetColor();

            // Show only failures/disabled subsystems
            foreach (var (name, _, detail) in failed)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                var msg = detail != null ? $"    ○ {name}: {detail}" : $"    ○ {name}";
                Console.WriteLine(msg);
                Console.ResetColor();
            }

            Console.WriteLine();
        }
    }

    // ── Conversation output ────────────────────────────────────

    public void WriteResponse(string personaName, string text)
    {
        lock (_lock)
        {
            Console.WriteLine($"\n  {personaName}: {text}");
        }
    }

    public void WriteSystem(string text)
    {
        if (Verbosity == OutputVerbosity.Quiet) return;

        lock (_lock)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  {text}");
            Console.ResetColor();
        }
    }

    // ── Debug / diagnostic output ──────────────────────────────

    public void WriteDebug(string text)
    {
        if (Verbosity < OutputVerbosity.Verbose) return;

        lock (_lock)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  {text}");
            Console.ResetColor();
        }
    }

    public void WriteWarning(string text)
    {
        if (Verbosity == OutputVerbosity.Quiet) return;

        lock (_lock)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  ⚠ {text}");
            Console.ResetColor();
        }
    }

    public void WriteError(string text)
    {
        lock (_lock)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ✗ {text}");
            Console.ResetColor();
        }
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

        if (Verbosity == OutputVerbosity.Verbose)
        {
            lock (_lock)
            {
                Console.WriteLine();
                Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║          🐍 OUROBOROS - Unified AI Agent System           ║");
                Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
                Console.WriteLine();
            }
            return;
        }

        // Normal mode: single subtle line
        lock (_lock)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            var moodPart = mood != null ? $" · mood: {mood}" : "";
            Console.WriteLine($"  Ouroboros v2 — {personaName} · {model}{moodPart} · help | status | exit");
            Console.ResetColor();
            Console.WriteLine();
        }
    }
}