using Spectre.Console;
using Ouroboros.CLI.Abstractions;
using Ouroboros.CLI.Infrastructure;
using Rule = Spectre.Console.Rule;

namespace Ouroboros.CLI.Commands.Handlers;

/// <summary>
/// Handler for the 'quality' command — renders a rich Spectre.Console
/// product-quality and consistency dashboard for the Ouroboros project.
/// </summary>
public sealed class QualityCommandHandler : ICommandHandler
{
    private readonly ISpectreConsoleService _console;

    public QualityCommandHandler(ISpectreConsoleService console)
    {
        _console = console;
    }

    public Task<int> HandleAsync(CancellationToken cancellationToken = default)
    {
        var c = _console.Console;

        RenderHeader(c);
        RenderOverallHealth(c);
        RenderLayerStatus(c);
        RenderSubsystemCompletion(c);
        RenderTestCoverage(c);
        RenderCriticalIssues(c);
        RenderConsistencyIssues(c);
        RenderTopPriorities(c);
        RenderFooter(c);

        return Task.FromResult(0);
    }

    // ─── Sections ────────────────────────────────────────────────────────────

    private static void RenderHeader(IAnsiConsole c)
    {
        c.WriteLine();
        c.Write(new FigletText("OUROBOROS")
            .Centered()
            .Color(new Color(99, 179, 237)));

        c.Write(new Rule("[bold cyan]Product Quality & Consistency Report[/]")
            .RuleStyle("cyan dim")
            .Centered());

        c.MarkupLine($"[dim]  Generated: [/][cyan]{DateTime.Today:yyyy-MM-dd}[/]" +
                     $"[dim]   ·   Repos: foundation / engine / app / build[/]");
        c.WriteLine();
    }

    private static void RenderOverallHealth(IAnsiConsole c)
    {
        c.Write(new Rule("[yellow bold] Overall Health Score [/]").RuleStyle("yellow dim"));
        c.WriteLine();

        // Big score display
        var scorePanel = new Panel(
            Align.Center(
                new Markup("[bold yellow]7[/][dim yellow] / 10[/]"),
                VerticalAlignment.Middle))
        {
            Width = 14,
            Height = 3,
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Yellow),
        };

        var barMarkup = BuildBar(70, 40, Color.Yellow, Color.Grey23);
        var healthGrid = new Grid();
        healthGrid.AddColumn(new GridColumn().Width(14));
        healthGrid.AddColumn(new GridColumn());
        healthGrid.AddRow(
            scorePanel,
            new Markup(
                $"\n  {barMarkup}  [yellow]70 %[/]\n\n" +
                "  [green]Strengths:[/] Excellent foundation layer, clean architecture,\n" +
                "             strong ethics framework, robust CI/CD\n\n" +
                "  [red]Weaknesses:[/] Advanced AI features are stubs, inconsistent\n" +
                "              error handling, placeholder copyrights"));

        c.Write(healthGrid);
        c.WriteLine();
    }

    private static void RenderLayerStatus(IAnsiConsole c)
    {
        c.Write(new Rule("[blue bold] Layer Readiness [/]").RuleStyle("blue dim"));
        c.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue)
            .AddColumn(new TableColumn("[bold]Layer[/]").Width(14))
            .AddColumn(new TableColumn("[bold]Status[/]").Centered().Width(22))
            .AddColumn(new TableColumn("[bold]Tests[/]").Centered().Width(16))
            .AddColumn(new TableColumn("[bold]Notes[/]"));

        table.AddRow(
            "[bold green]Foundation[/]",
            $"[green]{StatusBar(92)}[/]  [green bold]PRODUCTION[/]",
            "[green]409 / 0 fail[/]",
            "Core complete: monads, ethics, MeTTa, causal, genetic, Laws of Form\n[dim]Stubs: SelfModification (GitReflection) · Vectors (VectorStoreFactory)[/]");

        table.AddRow(
            "[bold yellow]Engine[/]",
            $"[yellow]{StatusBar(70)}[/]  [yellow bold]PARTIAL[/]",
            "[yellow]~2 556 / 26 fail[/]",
            "Pipeline ✓  Providers ✓  Theory of Mind ✗  Meta-AI ✗");

        table.AddRow(
            "[bold cyan]App[/]",
            $"[cyan]{StatusBar(85)}[/]  [cyan bold]BETA[/]",
            "[cyan]Multiple suites[/]",
            "CLI ✓  WebAPI ✓  Android ✓  Voice / Git TODOs");

        c.Write(table);
        c.WriteLine();
    }

    private static void RenderSubsystemCompletion(IAnsiConsole c)
    {
        c.Write(new Rule("[magenta bold] Subsystem Completion [/]").RuleStyle("magenta dim"));
        c.WriteLine();

        var chart = new BarChart()
            .Width(70)
            .Label("[bold]App-layer subsystems (%)[/]")
            .CenterLabel()
            .AddItem("Tools",        95, Color.Green)
            .AddItem("Memory",       85, Color.Green3)
            .AddItem("Cognitive",    80, Color.Yellow3)
            .AddItem("Autonomy",     70, Color.Yellow)
            .AddItem("Self-Assembly",70, Color.Orange3)
            .AddItem("Embodiment",   60, Color.Red3);

        c.Write(chart);
        c.WriteLine();

        // Engine advanced features
        var engineChart = new BarChart()
            .Width(70)
            .Label("[bold]Engine advanced features (%)[/]")
            .CenterLabel()
            .AddItem("Pipeline / Kleisli",     100, Color.Green)
            .AddItem("Providers (OpenAI…)",     90, Color.Green3)
            .AddItem("Symbolic KB",             55, Color.Orange3)
            .AddItem("World Model",             35, Color.Red)
            .AddItem("Theory of Mind",          20, Color.Red)
            .AddItem("Meta-Learning",           15, Color.Red);

        c.Write(engineChart);
        c.WriteLine();
    }

    private static void RenderTestCoverage(IAnsiConsole c)
    {
        c.Write(new Rule("[green bold] Test Coverage [/]").RuleStyle("green dim"));
        c.WriteLine();

        var table = new Table()
            .Border(TableBorder.Simple)
            .BorderColor(Color.Green)
            .AddColumn(new TableColumn("[bold]Repo[/]").Width(16))
            .AddColumn(new TableColumn("[bold]Total[/]").Centered())
            .AddColumn(new TableColumn("[bold]Failures[/]").Centered())
            .AddColumn(new TableColumn("[bold]Quality[/]").Centered())
            .AddColumn(new TableColumn("[bold]Coverage type[/]"));

        table.AddRow("[green]foundation[/]", "409",    "[green]0[/]",  "[green]★★★★★[/]", "Unit · Property-based (FsCheck) · BDD (Reqnroll)");
        table.AddRow("[yellow]engine[/]",    "~2 556", "[red]26 ✱[/]", "[yellow]★★★☆☆[/]", "Unit · Integration · ✱ pre-existing (API/Network)");
        table.AddRow("[cyan]app[/]",         "Multiple","[cyan]—[/]",  "[cyan]★★★☆☆[/]", "CLI · Application · Integration");

        c.Write(table);
        c.MarkupLine("[dim]  ✱ pre-existing failures are NOT caused by build changes (Providers / Safety / Network / Meta)[/]");
        c.WriteLine();
    }

    private static void RenderCriticalIssues(IAnsiConsole c)
    {
        c.Write(new Rule("[red bold] Critical Issues [/]").RuleStyle("red dim"));
        c.WriteLine();

        var issues = new[]
        {
            new Issue(
                Severity.Critical,
                "Theory of Mind — 5 unimplemented methods",
                "ouroboros-engine/src/Ouroboros.Agent/Agent/TheoryOfMind/TheoryOfMind.cs",
                "PredictBehaviorAsync, ModelGoalsAsync, InferIntentionsAsync, PredictActionAsync + 1 all throw NotImplementedException. Multi-agent reasoning completely non-functional."),
            new Issue(
                Severity.Critical,
                "Human Approval Workflow missing",
                "ouroboros-engine/src/Ouroboros.Agent/Agent/MetaAI/MetaAIPlannerOrchestrator.cs:123",
                "All plans that reach EthicalClearanceLevel.RequiresHumanApproval return Failure immediately. Ethics-gated decisions are permanently blocked."),
            new Issue(
                Severity.High,
                "World Model — Transformer/GNN/Hybrid not implemented",
                "ouroboros-engine/src/Ouroboros.Agent/Agent/MetaAI/WorldModel/WorldModelEngine.cs:52\n  ouroboros-engine/src/Ouroboros.Agent/Agent/WorldModel/WorldModel.cs",
                "Only MLP architecture works. Transformer, GNN, Hybrid all throw NotImplementedException. Complex environment modelling is severely limited."),
            new Issue(
                Severity.High,
                "Meta-Learning algorithm selection throws",
                "ouroboros-engine/src/Ouroboros.Agent/Agent/MetaLearning/MetaLearningEngine.cs",
                "Catch-all in pattern match throws NotImplementedException for unknown algorithms. Adaptive strategy selection non-functional."),
            new Issue(
                Severity.Medium,
                "GA Speed Scoring — hardcoded 0.7 placeholder",
                "ouroboros-engine/src/Ouroboros.Agent/Agent/MetaAI/Evolution/PlanStrategyFitness.cs:149",
                "OuroborosExperience lacks Duration tracking so speed fitness returns a constant. Genetic algorithm cannot optimise execution time."),
            new Issue(
                Severity.Medium,
                "Voice integration setup incomplete",
                "ouroboros-app/src/Ouroboros.CLI/Infrastructure/VoiceIntegrationService.cs",
                "TODO comment for voice setup; voice I/O partially functional."),
            new Issue(
                Severity.Medium,
                "Foundation: GitReflectionService — self-modification stubs",
                "ouroboros-foundation/src/Ouroboros.Domain/Domain/SelfModification/GitReflectionService.cs",
                "NotImplementedException in the SelfModification subsystem. Self-repair / self-evolution via git is non-functional even in the foundation layer."),
            new Issue(
                Severity.Medium,
                "Foundation: VectorStoreFactory — store creation stubs",
                "ouroboros-foundation/src/Ouroboros.Domain/Domain/Vectors/VectorStoreFactory.cs",
                "NotImplementedException in VectorStoreFactory. Vector store construction is incomplete; any feature requiring a dynamically-created store will fail at runtime."),
            new Issue(
                Severity.Low,
                "50+ PlaceholderCompany copyright headers",
                "ouroboros-engine/**  ouroboros-app/**",
                "Scaffolded copyright headers never replaced; looks unprofessional in any public-facing context."),
        };

        foreach (var issue in issues)
        {
            var color = issue.Severity switch
            {
                Severity.Critical => Color.Red,
                Severity.High     => Color.OrangeRed1,
                Severity.Medium   => Color.Yellow,
                _                 => Color.Grey,
            };

            var badge = issue.Severity switch
            {
                Severity.Critical => "[red bold on white] CRITICAL [/]",
                Severity.High     => "[white bold on red3] HIGH [/]",
                Severity.Medium   => "[black bold on yellow] MEDIUM [/]",
                _                 => "[white bold on grey] LOW [/]",
            };

            var content = new Markup(
                $"{badge}  [bold white]{Markup.Escape(issue.Title)}[/]\n" +
                $"  [dim link]{Markup.Escape(issue.FilePath)}[/]\n" +
                $"  [dim]{Markup.Escape(issue.Description)}[/]");

            c.Write(new Panel(content)
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(color),
                Padding = new Padding(1, 0),
            });
            c.WriteLine();
        }
    }

    private static void RenderConsistencyIssues(IAnsiConsole c)
    {
        c.Write(new Rule("[orange3 bold] Consistency Issues [/]").RuleStyle("orange3 dim"));
        c.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Orange3)
            .AddColumn(new TableColumn("[bold]Category[/]").Width(22))
            .AddColumn(new TableColumn("[bold]Finding[/]"));

        table.AddRow(
            "[yellow]Error handling[/]",
            "Foundation: pure [cyan]Result<T,E>[/] everywhere.\n" +
            "Engine: mix of Result + exceptions.\n" +
            "App: mostly exceptions with Result wrappers.\n" +
            "[dim]→ Inconsistent error propagation across layers.[/]");

        table.AddRow(
            "[yellow]Data model strategy[/]",
            "Foundation: immutable [cyan]record[/] types.\n" +
            "Engine: mutable [cyan]class[/] with state buffers.\n" +
            "App: both, depending on context.\n" +
            "[dim]→ No unified domain aggregate strategy.[/]");

        table.AddRow(
            "[yellow]Feature flags[/]",
            "App: [cyan]Config.Enable*[/] properties guard each subsystem.\n" +
            "Engine: no graceful degradation if dependencies missing.\n" +
            "[dim]→ Engine can hard-crash where App would skip gracefully.[/]");

        table.AddRow(
            "[yellow]Async patterns[/]",
            "Mostly async/await throughout (good).\n" +
            "Some synchronous code paths in agent core logic.\n" +
            "[dim]→ Risk of blocking the thread-pool under load.[/]");

        c.Write(table);
        c.WriteLine();
    }

    private static void RenderTopPriorities(IAnsiConsole c)
    {
        c.Write(new Rule("[cyan bold] Top Priorities [/]").RuleStyle("cyan dim"));
        c.WriteLine();

        var priorities = new[]
        {
            ("Theory of Mind prediction methods",        "Implement 5 stub methods — unblocks multi-agent reasoning",                                    Color.Red),
            ("Human approval workflow",                  "Add review mechanism so ethics-gated plans can actually proceed",                               Color.OrangeRed1),
            ("Duration tracking in OuroborosExperience","Enables GA speed scoring; currently a hardcoded 0.7 placeholder",                               Color.Orange3),
            ("World Model architectures",                "Add Transformer (minimum) to WorldModelEngine for complex environments",                        Color.Yellow3),
            ("Unify error handling",                     "Propagate Result<T,E> through Engine and App instead of mixing with exceptions",                Color.Yellow),
            ("Fix PlaceholderCompany headers",           "Replace 50+ copyright headers in engine/app with real attribution",                             Color.Olive),
            ("Integration test layer boundaries",        "Add tests that verify Foundation→Engine and Engine→App contracts end-to-end",                   Color.Grey),
        };

        var tree = new Tree("[bold]Implementation Roadmap[/]")
        {
            Style = new Style(Color.Cyan1),
            Guide = TreeGuide.Line,
        };

        for (int i = 0; i < priorities.Length; i++)
        {
            var (title, detail, color) = priorities[i];
            var label = $"[{ColorToMarkup(color)} bold]{i + 1}. {Markup.Escape(title)}[/]  [dim]{Markup.Escape(detail)}[/]";
            tree.AddNode(new Markup(label));
        }

        c.Write(tree);
        c.WriteLine();
    }

    private static void RenderFooter(IAnsiConsole c)
    {
        c.Write(new Rule().RuleStyle("grey dim"));
        c.MarkupLine("[dim]  Run [cyan]ouroboros quality[/] any time to re-display this dashboard.[/]");
        c.MarkupLine("[dim]  Explore critical files directly — paths are shown above each issue.[/]");
        c.WriteLine();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>Builds a filled/empty Unicode block bar for inline markup.</summary>
    private static string BuildBar(int percent, int width, Color fill, Color empty)
    {
        int filled = (int)Math.Round(percent / 100.0 * width);
        int rest = width - filled;
        return $"[{ColorToMarkup(fill)}]{new string('█', filled)}[/][{ColorToMarkup(empty)}]{new string('░', rest)}[/]";
    }

    /// <summary>Compact 20-char status bar for the layer table.</summary>
    private static string StatusBar(int percent)
    {
        int filled = (int)Math.Round(percent / 100.0 * 20);
        return new string('█', filled) + new string('░', 20 - filled) + $"  {percent}%";
    }

    private static string ColorToMarkup(Color color)
    {
        // Spectre.Console accepts "rgb(r,g,b)" in markup strings
        return $"rgb({color.R},{color.G},{color.B})";
    }

    // ─── Inner types ──────────────────────────────────────────────────────────

    private enum Severity { Critical, High, Medium, Low }

    private sealed record Issue(Severity Severity, string Title, string FilePath, string Description);
}
