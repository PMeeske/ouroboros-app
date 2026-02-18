namespace Ouroboros.CLI.Commands;

using Spectre.Console;
using Ouroboros.CLI.Services;

/// <summary>
/// Lightweight conversational REPL — the simplest way to talk to Ouroboros.
/// Inspired by Crush's low-friction chat loop: no flags required, just type.
/// </summary>
public static class ChatCommand
{
    public static async Task RunAsync(
        IAskService askService,
        IAnsiConsole console,
        CancellationToken ct)
    {
        // ── Welcome banner ─────────────────────────────────────
        console.Write(new Rule("[green]Ouroboros Chat[/]").RuleStyle("dim"));
        console.MarkupLine("[dim]Type a question and press Enter. Commands:[/]");
        console.MarkupLine("[dim]  [yellow]/rag[/]    Toggle RAG context   [yellow]/clear[/]  Clear screen[/]");
        console.MarkupLine("[dim]  [yellow]/model[/]  Change model          [yellow]/help[/]   Show commands[/]");
        console.MarkupLine("[dim]  [yellow]/exit[/]   Quit                                     [/]");
        console.WriteLine();

        var ragEnabled = false;
        var turnCount = 0;

        while (!ct.IsCancellationRequested)
        {
            // ── Prompt ─────────────────────────────────────────
            var prompt = ragEnabled
                ? "[green bold]you (rag)[/] [dim]>[/] "
                : "[green bold]you[/] [dim]>[/] ";

            string input;
            try
            {
                input = console.Prompt(
                    new TextPrompt<string>(prompt)
                        .AllowEmpty()
                        .PromptStyle("green"));
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(input))
                continue;

            // ── Slash commands ──────────────────────────────────
            if (input.StartsWith('/'))
            {
                switch (input.TrimEnd().ToLowerInvariant())
                {
                    case "/exit" or "/quit" or "/q":
                        console.MarkupLine("[dim]Goodbye.[/]");
                        return;

                    case "/clear":
                        console.Clear();
                        console.Write(new Rule("[green]Ouroboros Chat[/]").RuleStyle("dim"));
                        continue;

                    case "/rag":
                        ragEnabled = !ragEnabled;
                        console.MarkupLine(ragEnabled
                            ? "[cyan]RAG enabled[/] — answers will include local file context."
                            : "[cyan]RAG disabled[/] — pure LLM answers.");
                        continue;

                    case "/help" or "/?":
                        PrintHelp(console);
                        continue;

                    case var cmd when cmd.StartsWith("/model"):
                        console.MarkupLine("[dim]Model switching is coming soon. Use [yellow]--model[/] with the ask command for now.[/]");
                        continue;

                    default:
                        console.MarkupLine($"[yellow]Unknown command:[/] {input}. Type [yellow]/help[/] for options.");
                        continue;
                }
            }

            // ── Ask the LLM ────────────────────────────────────
            turnCount++;
            try
            {
                var answer = string.Empty;
                await console.Status().StartAsync("[dim]Thinking...[/]", async ctx =>
                {
                    answer = await askService.AskAsync(input, ragEnabled);
                    ctx.Status = "Done";
                });

                // Display answer in a styled panel
                var panel = new Panel(Markup.Escape(answer.Trim()))
                    .Header("[cyan]ouroboros[/]")
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(new Style(Color.Green))
                    .Padding(1, 0, 1, 0);

                console.Write(panel);
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
            catch (Exception ex)
            {
                console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            }
        }

        // ── Session summary ────────────────────────────────────
        if (turnCount > 0)
        {
            console.Write(new Rule("[dim]Session ended[/]").RuleStyle("dim"));
            console.MarkupLine($"[dim]{turnCount} question{(turnCount == 1 ? "" : "s")} asked.[/]");
        }
    }

    private static void PrintHelp(IAnsiConsole console)
    {
        var table = new Table()
            .Border(TableBorder.Simple)
            .BorderStyle(new Style(Color.Green))
            .AddColumn("[green]Command[/]")
            .AddColumn("Description");

        table.AddRow("[yellow]/rag[/]", "Toggle Retrieval Augmented Generation on/off");
        table.AddRow("[yellow]/model[/]", "Switch the active LLM model");
        table.AddRow("[yellow]/clear[/]", "Clear the screen");
        table.AddRow("[yellow]/help[/]", "Show this help");
        table.AddRow("[yellow]/exit[/]", "End the chat session");
        table.AddRow("[dim]anything else[/]", "Send as a question to the LLM");

        console.Write(table);
    }
}
