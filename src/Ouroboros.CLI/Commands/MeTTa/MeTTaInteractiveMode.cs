// <copyright file="MeTTaInteractiveMode.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Abstractions;
using Ouroboros.Abstractions.Monads;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Spectre.Console;

namespace Ouroboros.CLI.Commands;

using Ouroboros.Pipeline.Planning;
using Ouroboros.Pipeline.Verification;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// Interactive REPL mode for MeTTa symbolic reasoning.
/// Provides a consistent interface for querying and plan checking.
/// </summary>
public static class MeTTaInteractiveMode
{
    /// <summary>
    /// Runs the interactive MeTTa REPL (Read-Eval-Print Loop).
    /// </summary>
    /// <param name="engine">The MeTTa engine to use.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task RunInteractiveAsync(IMeTTaEngine? engine = null)
    {
        // Initialize engine if not provided
        engine ??= new SubprocessMeTTaEngine();

        // Initialize plan selector
        var planSelector = new SymbolicPlanSelector(engine);
        await planSelector.InitializeAsync();

        PrintWelcome();

        bool running = true;
        while (running)
        {
            AnsiConsole.Markup($"\n{OuroborosTheme.GoldText("metta> ")}");
            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            string[] parts = input.Trim().Split(' ', 2);
            string command = parts[0].ToLowerInvariant();

            try
            {
                switch (command)
                {
                    case "help" or "?":
                        PrintHelp();
                        break;

                    case "query" or "q":
                        if (parts.Length < 2)
                        {
                            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Usage:")} query <metta-expression>");
                            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Example:")} query (+ 1 2)");
                        }
                        else
                        {
                            await ExecuteQueryAsync(engine, parts[1]);
                        }
                        break;

                    case "plan" or "p":
                        await ExecutePlanCheckInteractiveAsync(planSelector);
                        break;

                    case "fact" or "f":
                        if (parts.Length < 2)
                        {
                            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Usage:")} fact <metta-fact>");
                            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Example:")} fact (human Socrates)");
                        }
                        else
                        {
                            await AddFactAsync(engine, parts[1]);
                        }
                        break;

                    case "rule" or "r":
                        if (parts.Length < 2)
                        {
                            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Usage:")} rule <metta-rule>");
                            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Example:")} rule (= (mortal $x) (human $x))");
                        }
                        else
                        {
                            await ApplyRuleAsync(engine, parts[1]);
                        }
                        break;

                    case "reset":
                        await ResetEngineAsync(engine);
                        await planSelector.InitializeAsync();
                        break;

                    case "exit" or "quit" or "q!":
                        running = false;
                        AnsiConsole.MarkupLine(OuroborosTheme.Dim("Goodbye!"));
                        break;

                    default:
                        // Treat unknown commands as queries
                        await ExecuteQueryAsync(engine, input);
                        break;
                }
            }
            catch (InvalidOperationException ex)
            {
                AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"Error: {ex.Message}")}[/]");
            }
        }

        engine.Dispose();
    }

    private static void PrintWelcome()
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("MeTTa Interactive Symbolic Reasoning Mode"));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(OuroborosTheme.Accent("Phase 4 Integration"));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"Type {OuroborosTheme.GoldText("'help'")} for available commands, {OuroborosTheme.GoldText("'exit'")} to quit.");
    }

    private static void PrintHelp()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Available Commands"));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("help, ?")}           - Show this help message");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("query, q <expr>")}   - Execute a MeTTa query");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("fact, f <fact>")}    - Add a fact to the knowledge base");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("rule, r <rule>")}    - Apply a reasoning rule");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("plan, p")}           - Interactive plan constraint checking");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("reset")}             - Reset the knowledge base");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("exit, quit, q!")}    - Exit interactive mode");
        AnsiConsole.WriteLine();
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Examples"));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim("query (+ 1 2)")}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim("fact (human Socrates)")}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim("rule (= (mortal $x) (human $x))")}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim("query (mortal Socrates)")}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim("plan")}");
    }

    private static async Task ExecuteQueryAsync(IMeTTaEngine engine, string query)
    {
        Result<string, string> result = await engine.ExecuteQueryAsync(query, CancellationToken.None);

        result.Match(
            success =>
            {
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Result:")} {Markup.Escape(success)}");
                return Unit.Value;
            },
            error =>
            {
                AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"Error: {error}")}[/]");
                return Unit.Value;
            });
    }

    private static async Task AddFactAsync(IMeTTaEngine engine, string fact)
    {
        Result<Unit, string> result = await engine.AddFactAsync(fact, CancellationToken.None);

        result.Match(
            _ =>
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Ok("\u2713 Fact added"));
                return Unit.Value;
            },
            error =>
            {
                AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"Error: {error}")}[/]");
                return Unit.Value;
            });
    }

    private static async Task ApplyRuleAsync(IMeTTaEngine engine, string rule)
    {
        Result<string, string> result = await engine.ApplyRuleAsync(rule, CancellationToken.None);

        result.Match(
            success =>
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Ok($"\u2713 Rule applied: {success}"));
                return Unit.Value;
            },
            error =>
            {
                AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"Error: {error}")}[/]");
                return Unit.Value;
            });
    }

    private static async Task ResetEngineAsync(IMeTTaEngine engine)
    {
        Result<Unit, string> result = await engine.ResetAsync(CancellationToken.None);

        result.Match(
            _ =>
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Warn("\u2713 Knowledge base reset"));
                return Unit.Value;
            },
            error =>
            {
                AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"Error: {error}")}[/]");
                return Unit.Value;
            });
    }

    private static async Task ExecutePlanCheckInteractiveAsync(SymbolicPlanSelector selector)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Interactive Plan Constraint Checking"));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("Enter plan actions (one per line). Type 'done' when finished.");
        AnsiConsole.WriteLine();

        var actions = new List<PlanAction>();
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Available Action Types"));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("1.")} FileSystem <operation> <path>");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("2.")} Network <operation> <endpoint>");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("3.")} Tool <name> <args>");
        AnsiConsole.WriteLine();

        bool enteringActions = true;
        while (enteringActions)
        {
            AnsiConsole.Markup($"{OuroborosTheme.GoldText("action> ")}");
            string? actionInput = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(actionInput))
            {
                continue;
            }

            if (actionInput.Trim().Equals("done", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            // Parse action parts
            string[] actionParts = actionInput.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (actionParts.Length < 1)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Warn("Invalid action format. Try: <type> <operation> [<target>]"));
                continue;
            }

            // Parse action with validation
            PlanAction? action = null;

            switch (actionParts[0].ToLowerInvariant())
            {
                case "filesystem" or "fs":
                    if (actionParts.Length < 2)
                    {
                        AnsiConsole.MarkupLine(OuroborosTheme.Warn("FileSystem action requires operation (e.g., 'filesystem read')"));
                        continue;
                    }
                    action = new FileSystemAction(
                        actionParts[1],
                        actionParts.Length > 2 ? actionParts[2] : null);
                    break;

                case "network" or "net":
                    if (actionParts.Length < 2)
                    {
                        AnsiConsole.MarkupLine(OuroborosTheme.Warn("Network action requires operation (e.g., 'network get')"));
                        continue;
                    }
                    action = new NetworkAction(
                        actionParts[1],
                        actionParts.Length > 2 ? actionParts[2] : null);
                    break;

                case "tool":
                    if (actionParts.Length < 2)
                    {
                        AnsiConsole.MarkupLine(OuroborosTheme.Warn("Tool action requires name (e.g., 'tool search_tool')"));
                        continue;
                    }
                    action = new ToolAction(
                        actionParts[1],
                        actionParts.Length > 2 ? actionParts[2] : null);
                    break;

                default:
                    AnsiConsole.MarkupLine(OuroborosTheme.Warn("Unknown action type. Use: filesystem, network, or tool"));
                    continue;
            }

            if (action != null)
            {
                actions.Add(action);
                AnsiConsole.MarkupLine(OuroborosTheme.Ok($"\u2713 Added: {action.ToMeTTaAtom()}"));
            }
        }

        if (actions.Count == 0)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Dim("No actions entered. Returning to main prompt."));
            return;
        }

        AnsiConsole.Markup($"\n{OuroborosTheme.Accent("Enter plan description:")} ");
        string? description = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(description))
        {
            description = "User-defined plan";
        }

        // Build the plan
        var plan = new Ouroboros.Pipeline.Verification.Plan(description);
        foreach (var action in actions)
        {
            plan = plan.WithAction(action);
        }

        // Check against contexts
        var contexts = new[] { SafeContext.ReadOnly, SafeContext.FullAccess };

        foreach (var context in contexts)
        {
            AnsiConsole.MarkupLine($"\n{OuroborosTheme.Accent("Checking against")} {Markup.Escape(context.ToMeTTaAtom())} {OuroborosTheme.Accent("context:")}");

            Result<string, string> result = await selector.ExplainPlanAsync(
                plan,
                context,
                CancellationToken.None);

            result.Match(
                explanation =>
                {
                    AnsiConsole.MarkupLine($"  [rgb(148,103,189)]{Markup.Escape(explanation)}[/]");
                    return Unit.Value;
                },
                error =>
                {
                    AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"Error: {error}")}[/]");
                    return Unit.Value;
                });
        }
    }
}
