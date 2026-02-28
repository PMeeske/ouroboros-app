namespace Ouroboros.CLI.Commands;

using Spectre.Console;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Services;

/// <summary>
/// Lightweight conversational REPL — the simplest way to talk to Ouroboros.
///
/// Crush-inspired usability additions:
///   • Slash command palette  — type / to see commands; Tab to fuzzy-complete
///   • Prompt history         — Up / Down arrows navigate previous inputs
///   • Status bar             — model name + context-usage % after each turn
///   • Tool display           — ● / ✓ / ✗ icons via ToolRenderer
///   • Permission hook        — ToolPermissionBroker for future tool calls
///   • Extensible registry    — subsystems can Register() their own commands
/// </summary>
public static class ChatCommand
{
    // ── Default command registry ──────────────────────────────────────────────

    /// <summary>
    /// Builds the default slash command registry. Callers may augment it before
    /// passing it to <see cref="RunAsync"/>.
    /// </summary>
    public static SlashCommandRegistry BuildDefaultRegistry(IAnsiConsole console) =>
        new SlashCommandRegistry()
            .Register("help",    "Show available slash commands",         shortcut: "ctrl+g",
                execute: (_, _) =>
                {
                    PrintHelp(console, null!);
                    return Task.FromResult(SlashCommandResult.Handled());
                })
            .Register("clear",   "Clear the screen",                     shortcut: "ctrl+l",
                execute: (_, _) =>
                {
                    console.Clear();
                    console.Write(new Rule("[green]Ouroboros Chat[/]").RuleStyle("dim"));
                    return Task.FromResult(SlashCommandResult.Handled());
                })
            .Register("rag",     "Toggle RAG context on / off")
            .Register("model",   "Show or switch the active LLM model",  shortcut: "ctrl+m")
            .Register("session", "List past chat sessions",               shortcut: "ctrl+s")
            .Register("yolo",    "Auto-approve all tool calls this session")
            .Register("exit",    "End the session",                      shortcut: "ctrl+c",
                execute: (_, _) => Task.FromResult(SlashCommandResult.Exit("Goodbye.")));

    // ── Entry point ───────────────────────────────────────────────────────────

    public static async Task RunAsync(
        IAskService askService,
        IAnsiConsole console,
        CancellationToken ct,
        SlashCommandRegistry? registry = null,
        ToolPermissionBroker? permissionBroker = null)
    {
        registry ??= BuildDefaultRegistry(console);
        permissionBroker ??= new ToolPermissionBroker();

        var history = new PromptHistory();
        var ragEnabled = false;
        var turnCount = 0;
        var model = "ollama";

        // ── Welcome banner ────────────────────────────────────────────────────
        console.Write(new Rule("[green]Ouroboros Chat[/]").RuleStyle("dim"));
        console.MarkupLine("[dim]Type to chat. [yellow]/[/] for commands, [yellow]Tab[/] to complete, [yellow]↑↓[/] for history.[/]");
        console.WriteLine();
        PrintHelp(console, registry);
        console.WriteLine();
        WriteStatusBar(console, model, workingDir: Directory.GetCurrentDirectory(), contextPct: 0);
        console.WriteLine();

        // ── REPL loop ─────────────────────────────────────────────────────────
        while (!ct.IsCancellationRequested)
        {
            var promptText = ragEnabled
                ? "[green bold]you (rag)[/] [dim]>[/] "
                : "[green bold]you[/] [dim]>[/] ";

            string input;
            try
            {
                input = ReadWithHistory(console, promptText, history, registry);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(input))
                continue;

            // ── Slash commands ────────────────────────────────────────────────
            if (input.StartsWith('/'))
            {
                var tokens = input.Split(' ', 2);
                var cmdName = tokens[0][1..];

                // Stateful commands that need local REPL context
                switch (cmdName.ToLowerInvariant())
                {
                    case "rag":
                        ragEnabled = !ragEnabled;
                        console.MarkupLine(ragEnabled
                            ? "  [cyan]RAG enabled[/] — answers will include local file context."
                            : "  [cyan]RAG disabled[/] — pure LLM answers.");
                        continue;

                    case "yolo":
                        permissionBroker.SkipAll = true;
                        console.MarkupLine("  [yellow]YOLO mode:[/] all tool calls auto-approved for this session.");
                        continue;

                    case "session":
                        console.MarkupLine("  [dim]Session history coming soon. (ctrl+s)[/]");
                        continue;

                    case "model":
                        if (tokens.Length > 1 && !string.IsNullOrWhiteSpace(tokens[1]))
                        {
                            model = tokens[1].Trim();
                            console.MarkupLine($"  [cyan]Model set to:[/] {Markup.Escape(model)}");
                        }
                        else
                        {
                            console.MarkupLine($"  Current model: [cyan]{Markup.Escape(model)}[/]");
                            console.MarkupLine("  [dim]Usage: /model <name>[/]");
                        }
                        continue;
                }

                // Dispatch via registry
                var result = await registry.DispatchAsync(input, ct);
                if (result is null)
                {
                    var suggestions = registry.Filter(cmdName);
                    console.MarkupLine($"  [yellow]Unknown command:[/] {Markup.Escape(input)}");
                    if (suggestions.Count > 0)
                    {
                        console.MarkupLine("  [dim]Did you mean?[/]");
                        foreach (var s in suggestions.Take(3))
                            console.MarkupLine($"    [yellow]/{Markup.Escape(s.Name)}[/]  {Markup.Escape(s.Description)}");
                    }
                    continue;
                }

                if (!string.IsNullOrEmpty(result.Output))
                    console.MarkupLine($"  {Markup.Escape(result.Output)}");

                if (result.ShouldExit) return;
                continue;
            }

            // ── Save to history before sending ────────────────────────────────
            history.Push(input);

            // ── Ask the LLM ───────────────────────────────────────────────────
            turnCount++;
            try
            {
                var answer = string.Empty;
                await console.Status().StartAsync("[dim]Thinking...[/]", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    answer = await askService.AskAsync(input, ragEnabled);
                    ctx.Status = "Done";
                });

                var panel = new Panel(Markup.Escape(answer.Trim()))
                    .Header("[cyan]ouroboros[/]")
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(new Style(Color.Green))
                    .Padding(1, 0, 1, 0);

                console.Write(panel);
                console.WriteLine();

                // Status bar — rough token/context estimate (4 chars ≈ 1 token, 128k budget)
                var roughTokens = (input.Length + answer.Length) / 4;
                var pct = Math.Min(99, turnCount * roughTokens / 1280);
                WriteStatusBar(console, model, workingDir: null, contextPct: pct);
                console.WriteLine();
            }
            catch (HttpRequestException ex)
            {
                console.MarkupLine("[red]Could not reach the LLM endpoint.[/]");
                console.MarkupLine($"[dim]  {Markup.Escape(ex.Message)}[/]");
                console.MarkupLine("[dim]  Ensure Ollama is running ([yellow]ollama serve[/]) or check your endpoint.[/]");
            }
            catch (TaskCanceledException)
            {
                console.MarkupLine("[yellow]Request timed out.[/] The model may still be loading — try again.");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            }
        }

        // ── Session summary ───────────────────────────────────────────────────
        if (turnCount > 0)
        {
            console.Write(new Rule("[dim]Session ended[/]").RuleStyle("dim"));
            console.MarkupLine($"[dim]{turnCount} message{(turnCount == 1 ? "" : "s")} sent.[/]");
        }
    }

    // ── Prompt input with history navigation ──────────────────────────────────

    /// <summary>
    /// Reads a line with Up/Down arrow history navigation and Tab slash-completion.
    /// Prints the Spectre markup prompt then reads raw key events from Console.
    /// </summary>
    private static string ReadWithHistory(
        IAnsiConsole console,
        string promptMarkup,
        PromptHistory history,
        SlashCommandRegistry registry)
    {
        console.Markup(promptMarkup);
        var buffer = new System.Text.StringBuilder();

        while (true)
        {
            ConsoleKeyInfo key;
            try { key = Console.ReadKey(intercept: true); }
            catch (InvalidOperationException) { return buffer.ToString(); }

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    Console.WriteLine();
                    return buffer.ToString();

                case ConsoleKey.UpArrow:
                {
                    var prev = history.NavigateUp(buffer.ToString());
                    if (prev is not null) ReplaceConsoleLine(buffer, prev);
                    break;
                }

                case ConsoleKey.DownArrow:
                {
                    var next = history.NavigateDown();
                    ReplaceConsoleLine(buffer, next);
                    break;
                }

                case ConsoleKey.Backspace:
                    if (buffer.Length > 0)
                    {
                        buffer.Remove(buffer.Length - 1, 1);
                        Console.Write("\b \b");
                    }
                    break;

                case ConsoleKey.Escape:
                    if (history.IsNavigating)
                        ReplaceConsoleLine(buffer, history.CancelNavigation());
                    break;

                case ConsoleKey.Tab:
                    // Slash command completion when buffer starts with /
                    if (buffer.Length > 0 && buffer[0] == '/')
                    {
                        var query = buffer.ToString()[1..];
                        var matches = registry.Filter(query);
                        if (matches.Count == 1)
                        {
                            ReplaceConsoleLine(buffer, "/" + matches[0].Name + " ");
                        }
                        else if (matches.Count > 1)
                        {
                            Console.WriteLine();
                            foreach (var m in matches.Take(5))
                                Console.WriteLine($"  /{m.Name.PadRight(12)}  {m.Description}");
                            console.Markup(promptMarkup);
                            Console.Write(buffer.ToString());
                        }
                    }
                    break;

                default:
                    if (key.KeyChar != '\0' && !char.IsControl(key.KeyChar))
                    {
                        buffer.Append(key.KeyChar);
                        Console.Write(key.KeyChar);
                    }
                    break;
            }
        }
    }

    private static void ReplaceConsoleLine(System.Text.StringBuilder buffer, string text)
    {
        // Erase current line content, write new text
        Console.Write(new string('\b', buffer.Length));
        Console.Write(new string(' ', buffer.Length));
        Console.Write(new string('\b', buffer.Length));
        buffer.Clear();
        buffer.Append(text);
        Console.Write(text);
    }

    // ── Status bar ────────────────────────────────────────────────────────────

    private static void WriteStatusBar(IAnsiConsole console, string model, string? workingDir, int? contextPct)
    {
        var parts = new List<string> { $"[dim]{Markup.Escape(model)}[/]" };

        if (workingDir is not null)
            parts.Add($"[dim]{Markup.Escape(TruncatePath(workingDir, 28))}[/]");

        var bar = string.Join(" [dim]·[/] ", parts);

        if (contextPct.HasValue)
        {
            var color = contextPct.Value switch { >= 80 => "red", >= 50 => "yellow", _ => "dim" };
            bar += $"  [{color}]{contextPct.Value}%[/]";
        }

        console.MarkupLine($"  {bar}");
    }

    // ── Help table ────────────────────────────────────────────────────────────

    private static void PrintHelp(IAnsiConsole console, SlashCommandRegistry registry)
    {
        var table = new Table()
            .Border(TableBorder.Simple)
            .BorderStyle(new Style(Color.Green))
            .AddColumn("[green]Command[/]")
            .AddColumn("Description")
            .AddColumn("[dim]Shortcut[/]");

        var commands = registry?.All ?? BuildDefaultRegistry(console).All;
        foreach (var cmd in commands)
        {
            table.AddRow(
                $"[yellow]/{Markup.Escape(cmd.Name)}[/]",
                Markup.Escape(cmd.Description),
                cmd.Shortcut is not null ? $"[dim]{Markup.Escape(cmd.Shortcut)}[/]" : "");
        }

        table.AddRow("[dim]<message>[/]", "Send a question to the LLM",   "[dim]Enter[/]");
        table.AddRow("[dim]↑ / ↓[/]",    "Navigate prompt history",        "");
        table.AddRow("[dim]Tab[/]",       "Auto-complete slash command",    "");
        console.Write(table);
    }

    private static string TruncatePath(string path, int max)
    {
        if (path.Length <= max) return path;
        var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? "…/" + parts[^1] : "…" + path[^(max - 1)..];
    }
}
