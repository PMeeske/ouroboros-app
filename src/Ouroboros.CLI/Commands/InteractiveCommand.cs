namespace Ouroboros.CLI.Commands;

using Spectre.Console;
using Ouroboros.CLI.Services;

/// <summary>
/// Guided command launcher — users discover features through selection prompts
/// instead of memorising flags. Inspired by Crush's progressive-disclosure UX.
/// </summary>
public static class InteractiveCommand
{
    public static async Task RunAsync(
        IAskService askService,
        IPipelineService pipelineService,
        IAnsiConsole console,
        CancellationToken ct)
    {
        console.Write(new FigletText("Ouroboros").Color(Color.Green));
        console.Write(new Rule("[dim]Interactive Mode[/]").RuleStyle("green"));
        console.WriteLine();

        while (!ct.IsCancellationRequested)
        {
            var choice = console.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]What would you like to do?[/]")
                    .PageSize(10)
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoiceGroup("[green]Ask & Chat[/]", [
                        "Ask a question",
                        "Start a chat session",
                    ])
                    .AddChoiceGroup("[green]Pipelines[/]", [
                        "Build a pipeline (guided)",
                        "Run a DSL pipeline",
                    ])
                    .AddChoiceGroup("[green]Tools[/]", [
                        "Check environment (doctor)",
                        "Guided setup wizard",
                    ])
                    .AddChoices(["Exit"]));

            switch (choice)
            {
                case "Ask a question":
                    await HandleAskAsync(askService, console);
                    break;

                case "Start a chat session":
                    await ChatCommand.RunAsync(askService, console, ct);
                    break;

                case "Build a pipeline (guided)":
                    await HandleGuidedPipelineAsync(pipelineService, console);
                    break;

                case "Run a DSL pipeline":
                    await HandleRawPipelineAsync(pipelineService, console);
                    break;

                case "Check environment (doctor)":
                    await DoctorCommand.RunAsync(console);
                    break;

                case "Guided setup wizard":
                    console.MarkupLine("[dim]Launching guided setup...[/]");
                    await Setup.GuidedSetup.RunAsync(new SetupOptions());
                    break;

                case "Exit":
                    console.MarkupLine("[dim]Goodbye.[/]");
                    return;
            }

            console.WriteLine();
        }
    }

    // ── Ask (one-shot) ─────────────────────────────────────────
    private static async Task HandleAskAsync(IAskService askService, IAnsiConsole console)
    {
        var question = console.Prompt(
            new TextPrompt<string>("[green]Your question:[/]")
                .PromptStyle("cyan"));

        var useRag = console.Confirm("[dim]Include local file context (RAG)?[/]", false);

        try
        {
            var answer = string.Empty;
            await console.Status().StartAsync("[dim]Thinking...[/]", async ctx =>
            {
                answer = await askService.AskAsync(question, useRag);
                ctx.Status = "Done";
            });

            console.Write(
                new Panel(Markup.Escape(answer.Trim()))
                    .Header("[cyan]Answer[/]")
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(new Style(Color.Green))
                    .Padding(1, 0, 1, 0));
        }
        catch (Exception ex)
        {
            console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
        }
    }

    // ── Guided pipeline builder ────────────────────────────────
    private static async Task HandleGuidedPipelineAsync(
        IPipelineService pipelineService,
        IAnsiConsole console)
    {
        console.Write(new Rule("[green]Pipeline Builder[/]").RuleStyle("dim"));
        console.MarkupLine("[dim]Build a pipeline step by step. Steps execute left to right.[/]");
        console.WriteLine();

        // Step 1 — Topic
        var topic = console.Prompt(
            new TextPrompt<string>("[green]Topic or subject:[/]")
                .DefaultValue("AI")
                .PromptStyle("cyan"));

        // Step 2 — Pipeline steps (multi-select)
        var steps = console.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("[green]Select pipeline steps[/] [dim](space to toggle, enter to confirm)[/]")
                .PageSize(12)
                .HighlightStyle(new Style(Color.Cyan1))
                .InstructionsText("[dim](use arrow keys, space to select, enter to confirm)[/]")
                .AddChoiceGroup("Generation", [
                    "UseDraft",
                    "UseElaborate",
                    "UseSummarize",
                ])
                .AddChoiceGroup("Refinement", [
                    "UseCritique",
                    "UseImprove",
                    "UseFactCheck",
                ])
                .AddChoiceGroup("Output", [
                    "UseFormat",
                    "UseTranslate",
                ]));

        // Step 3 — Build DSL string
        var dslParts = new List<string> { $"SetTopic('{Markup.Escape(topic)}')" };
        dslParts.AddRange(steps);
        var dsl = string.Join(" | ", dslParts);

        // Step 4 — Preview & confirm
        console.Write(
            new Panel($"[cyan]{Markup.Escape(dsl)}[/]")
                .Header("[green]Generated DSL[/]")
                .Border(BoxBorder.Rounded)
                .BorderStyle(new Style(Color.Green)));

        if (!console.Confirm("[green]Execute this pipeline?[/]"))
        {
            console.MarkupLine("[dim]Pipeline cancelled.[/]");
            return;
        }

        // Step 5 — Execute
        try
        {
            var result = string.Empty;
            await console.Status().StartAsync("[dim]Running pipeline...[/]", async ctx =>
            {
                result = await pipelineService.ExecutePipelineAsync(dsl);
                ctx.Status = "Done";
            });

            console.Write(
                new Panel(Markup.Escape(result.Trim()))
                    .Header("[cyan]Pipeline Result[/]")
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(new Style(Color.Green))
                    .Padding(1, 0, 1, 0));
        }
        catch (Exception ex)
        {
            console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
        }
    }

    // ── Raw DSL entry ──────────────────────────────────────────
    private static async Task HandleRawPipelineAsync(
        IPipelineService pipelineService,
        IAnsiConsole console)
    {
        var dsl = console.Prompt(
            new TextPrompt<string>("[green]DSL expression:[/]")
                .PromptStyle("cyan")
                .DefaultValue("SetTopic('AI') | UseDraft | UseCritique"));

        try
        {
            var result = string.Empty;
            await console.Status().StartAsync("[dim]Running pipeline...[/]", async ctx =>
            {
                result = await pipelineService.ExecutePipelineAsync(dsl);
                ctx.Status = "Done";
            });

            console.Write(
                new Panel(Markup.Escape(result.Trim()))
                    .Header("[cyan]Result[/]")
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(new Style(Color.Green))
                    .Padding(1, 0, 1, 0));
        }
        catch (Exception ex)
        {
            console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
        }
    }
}
