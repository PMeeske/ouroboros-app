using System.CommandLine;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Commands.Options;
using Ouroboros.CLI.Commands.Handlers;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Services;
using Ouroboros.CLI.Hosting;

// Create the host builder
var hostBuilder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Register CLI services
        services.AddCliServices();

        // Register command handlers
        services.AddCommandHandlers();

        // Register infrastructure
        services.AddInfrastructureServices();

        // Cognitive Physics Engine defaults (IEmbeddingProvider, IEthicsGate, config)
        services.AddCognitivePhysicsDefaults();

        // Register existing business logic
        services.AddExistingBusinessLogic();
    })
    .ConfigureLogging((context, logging) =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Warning);
    });

// Build the host
using var host = hostBuilder.Build();

// Create the root command with System.CommandLine
var rootCommand = new RootCommand("Ouroboros CLI - Advanced AI Assistant System");

// Add --version option
var versionOption = new System.CommandLine.Option<bool>("--version", "Show version information");
rootCommand.Add(versionOption);
rootCommand.SetAction((parseResult, _) =>
{
    if (parseResult.GetValue(versionOption))
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? assembly.GetName().Version?.ToString()
                      ?? "0.0.0";
        AnsiConsole.MarkupLine($"[cyan]Ouroboros CLI[/] {version}");
        AnsiConsole.MarkupLine($"[dim]Runtime:[/] {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
        AnsiConsole.MarkupLine($"[dim]OS:[/]      {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
        return Task.CompletedTask;
    }

    // No subcommand provided — show help
    parseResult.Configuration.Output.Write(parseResult.GetResult(rootCommand)!.Command.ToString());
    return Task.CompletedTask;
});

// Add global voice option (Recursive = true makes it propagate to all subcommands)
var voiceOption = new System.CommandLine.Option<bool>("--voice")
{
    Description = "Enable voice interaction mode",
    DefaultValueFactory = _ => false,
    Recursive = true
};
rootCommand.Add(voiceOption);

// Add subcommands
rootCommand.Add(CreateAskCommand(host, voiceOption));
rootCommand.Add(CreatePipelineCommand(host, voiceOption));
rootCommand.Add(CreateOuroborosCommand(host, voiceOption));
rootCommand.Add(CreateSkillsCommand(host, voiceOption));
rootCommand.Add(CreateOrchestratorCommand(host, voiceOption));
rootCommand.Add(CreateCognitivePhysicsCommand(host, voiceOption));
rootCommand.Add(CreateDoctorCommand());

// Parse and invoke
return await rootCommand.Parse(args).InvokeAsync();

// ────────────────────────────────────────────────────────
// Command creation methods
// ────────────────────────────────────────────────────────

static Command CreateAskCommand(IHost host, System.CommandLine.Option<bool> globalVoiceOption)
{
    var options = new AskCommandOptions();
    var command = new Command("ask", "Ask the LLM a question");

    // Add all options using the helper
    options.AddToCommand(command);

    // Configure handler via extension method
    return command.ConfigureAskCommand(host, options, globalVoiceOption);
}

static Command CreatePipelineCommand(IHost host, System.CommandLine.Option<bool> globalVoiceOption)
{
    var options = new PipelineCommandOptions();
    var command = new Command("pipeline", "Execute a DSL pipeline");

    // Add all options using the helper
    options.AddToCommand(command);

    command.SetAction(async (parseResult, cancellationToken) =>
    {
        var pipelineService = host.Services.GetRequiredService<IPipelineService>();
        var console = host.Services.GetRequiredService<ISpectreConsoleService>();
        var voiceService = host.Services.GetRequiredService<IVoiceIntegrationService>();

        var dsl = parseResult.GetValue(options.DslOption);
        var useVoice = parseResult.GetValue(globalVoiceOption);

        if (useVoice)
        {
            await voiceService.HandleVoiceCommandAsync("pipeline", ["--dsl", dsl ?? ""], cancellationToken);
            return;
        }

        await console.Status().StartAsync("Executing pipeline...", async ctx =>
        {
            var result = await pipelineService.ExecutePipelineAsync(dsl ?? "");
            ctx.Status = "Done";
            console.MarkupLine($"[green]Result:[/] {result}");
        });
    });

    return command;
}

static Command CreateOuroborosCommand(IHost host, System.CommandLine.Option<bool> globalVoiceOption)
{
    var options = new OuroborosCommandOptions();
    var command = new Command("ouroboros", "Run Ouroboros agent mode");

    // Add all options using the helper
    options.AddToCommand(command);

    // Configure handler via extension method (mirrors CreateAskCommand pattern)
    return command.ConfigureOuroborosCommand(host, options, globalVoiceOption);
}

static Command CreateSkillsCommand(IHost host, System.CommandLine.Option<bool> globalVoiceOption)
{
    var options = new SkillsCommandOptions();
    var command = new Command("skills", "Manage research skills");

    // Add all options using the helper
    options.AddToCommand(command);

    command.SetAction(async (parseResult, cancellationToken) =>
    {
        var skillsService = host.Services.GetRequiredService<ISkillsService>();
        var console = host.Services.GetRequiredService<ISpectreConsoleService>();
        var voiceService = host.Services.GetRequiredService<IVoiceIntegrationService>();

        var list = parseResult.GetValue(options.ListOption);
        var fetch = parseResult.GetValue(options.FetchOption);
        var useVoice = parseResult.GetValue(globalVoiceOption);

        if (useVoice)
        {
            var voiceArgs = new List<string>();
            if (list) voiceArgs.AddRange(["--list", "true"]);
            if (!string.IsNullOrEmpty(fetch)) voiceArgs.AddRange(["--fetch", fetch]);
            await voiceService.HandleVoiceCommandAsync("skills", voiceArgs.ToArray(), cancellationToken);
            return;
        }

        if (list)
        {
            var skills = await skillsService.ListSkillsAsync();
            var table = new Table();
            table.AddColumn("Name");
            table.AddColumn("Description");
            table.AddColumn("Success Rate");

            foreach (var skill in skills)
            {
                table.AddRow(skill.Name, skill.Description, $"{skill.SuccessRate:P0}");
            }

            console.Write(table);
        }
        else if (!string.IsNullOrEmpty(fetch))
        {
            await console.Status().StartAsync("Fetching research...", async ctx =>
            {
                var result = await skillsService.FetchAndExtractSkillAsync(fetch);
                ctx.Status = "Done";
                console.MarkupLine($"[green]Extracted skill:[/] {result}");
            });
        }
    });

    return command;
}

static Command CreateOrchestratorCommand(IHost host, System.CommandLine.Option<bool> globalVoiceOption)
{
    var options = new OrchestratorCommandOptions();
    var command = new Command("orchestrator", "Run multi-model orchestrator");

    // Add all options using the helper
    options.AddToCommand(command);

    command.SetAction(async (parseResult, cancellationToken) =>
    {
        var orchestratorService = host.Services.GetRequiredService<IOrchestratorService>();
        var console = host.Services.GetRequiredService<ISpectreConsoleService>();
        var voiceService = host.Services.GetRequiredService<IVoiceIntegrationService>();

        var goal = parseResult.GetValue(options.GoalOption);
        var useVoice = parseResult.GetValue(globalVoiceOption);

        if (useVoice)
        {
            await voiceService.HandleVoiceCommandAsync("orchestrator", ["--goal", goal ?? ""], cancellationToken);
            return;
        }

        await console.Status().StartAsync("Orchestrating models...", async ctx =>
        {
            var result = await orchestratorService.OrchestrateAsync(goal ?? "");
            ctx.Status = "Done";
            console.MarkupLine($"[green]Result:[/] {result}");
        });
    });

    return command;
}

static Command CreateCognitivePhysicsCommand(IHost host, System.CommandLine.Option<bool> globalVoiceOption)
{
    var options = new CognitivePhysicsCommandOptions();
    var command = new Command("cognitive-physics", "Execute Cognitive Physics Engine operations (ZeroShift, superposition, chaos, evolution)");

    // Add all options using the helper
    options.AddToCommand(command);

    command.SetAction(async (parseResult, cancellationToken) =>
    {
        var cpeService = host.Services.GetRequiredService<ICognitivePhysicsService>();
        var console = host.Services.GetRequiredService<ISpectreConsoleService>();

        var operation = parseResult.GetValue(options.OperationOption) ?? "shift";
        var focus     = parseResult.GetValue(options.FocusOption) ?? "general";
        var target    = parseResult.GetValue(options.TargetOption);
        var targets   = parseResult.GetValue(options.TargetsOption);
        var resources = parseResult.GetValue(options.ResourcesOption);
        var jsonOut   = parseResult.GetValue(options.JsonOutputOption);
        var verbose   = parseResult.GetValue(options.VerboseOption);

        switch (operation.ToLowerInvariant())
        {
            case "shift":
            {
                if (string.IsNullOrWhiteSpace(target))
                {
                    console.MarkupLine("[red]Error:[/] --target is required for shift operation.");
                    return;
                }

                await console.Status().StartAsync($"ZeroShift: {focus} → {target}...", async ctx =>
                {
                    var result = await cpeService.ShiftAsync(focus, target, resources);
                    ctx.Status = "Done";

                    if (result.IsSuccess)
                    {
                        var s = result.Value;
                        console.MarkupLine($"[green]Shift succeeded[/]");
                        console.MarkupLine($"  Focus:       [cyan]{s.Focus}[/]");
                        console.MarkupLine($"  Resources:   [yellow]{s.Resources:F1}[/]");
                        console.MarkupLine($"  Compression: [yellow]{s.Compression:F3}[/]");
                        console.MarkupLine($"  Cooldown:    [yellow]{s.Cooldown:F1}[/]");
                        if (verbose)
                            console.MarkupLine($"  History:     {string.Join(" → ", s.History)}");
                    }
                    else
                    {
                        console.MarkupLine($"[red]Shift failed:[/] {result.Error}");
                    }
                });
                break;
            }

            case "trajectory":
            {
                var targetList = targets?.ToList() ?? [];
                if (targetList.Count == 0)
                {
                    console.MarkupLine("[red]Error:[/] --targets is required for trajectory operation.");
                    return;
                }

                await console.Status().StartAsync($"Trajectory: {focus} → [{string.Join(" → ", targetList)}]...", async ctx =>
                {
                    var result = await cpeService.ExecuteTrajectoryAsync(focus, targetList, resources);
                    ctx.Status = "Done";

                    if (result.IsSuccess)
                    {
                        var s = result.Value;
                        console.MarkupLine($"[green]Trajectory completed[/]");
                        console.MarkupLine($"  Final Focus: [cyan]{s.Focus}[/]");
                        console.MarkupLine($"  Resources:   [yellow]{s.Resources:F1}[/]");
                        console.MarkupLine($"  Compression: [yellow]{s.Compression:F3}[/]");
                        if (verbose)
                            console.MarkupLine($"  Path:        {string.Join(" → ", s.History)}");
                    }
                    else
                    {
                        console.MarkupLine($"[red]Trajectory failed:[/] {result.Error}");
                    }
                });
                break;
            }

            case "entangle":
            {
                var branchTargets = targets?.ToList() ?? [];
                if (branchTargets.Count == 0)
                {
                    console.MarkupLine("[red]Error:[/] --targets is required for entangle operation.");
                    return;
                }

                await console.Status().StartAsync($"Entangling: {focus} → [{string.Join(", ", branchTargets)}]...", async ctx =>
                {
                    var branches = await cpeService.EntangleAsync(focus, branchTargets, resources);
                    ctx.Status = "Done";

                    console.MarkupLine($"[green]Entangled into {branches.Count} branches[/]");
                    var table = new Table();
                    table.AddColumn("Branch");
                    table.AddColumn("Focus");
                    table.AddColumn("Weight");
                    table.AddColumn("Resources");

                    for (int i = 0; i < branches.Count; i++)
                    {
                        var b = branches[i];
                        table.AddRow(
                            $"#{i + 1}",
                            b.State.Focus,
                            $"{b.Weight:F3}",
                            $"{b.State.Resources:F1}");
                    }

                    console.Write(table);
                });
                break;
            }

            case "chaos":
            {
                var result = cpeService.InjectChaos(focus, resources);

                if (result.IsSuccess)
                {
                    var s = result.Value;
                    console.MarkupLine($"[green]Chaos injected[/]");
                    console.MarkupLine($"  Focus:       [cyan]{s.Focus}[/]");
                    console.MarkupLine($"  Resources:   [yellow]{s.Resources:F1}[/]");
                    console.MarkupLine($"  Compression: [yellow]{s.Compression:F3}[/]");
                    if (verbose && s.Entanglement.Count > 0)
                        console.MarkupLine($"  Entangled:   {string.Join(", ", s.Entanglement)}");
                }
                else
                {
                    console.MarkupLine($"[red]Chaos injection failed:[/] {result.Error}");
                }
                break;
            }

            default:
                console.MarkupLine($"[red]Unknown operation:[/] {operation}. Use: shift, trajectory, entangle, chaos");
                break;
        }
    });

    return command;
}

static Command CreateDoctorCommand()
{
    var command = new Command("doctor", "Check your development environment for required and optional dependencies");

    command.SetAction(async (parseResult, cancellationToken) =>
    {
        await DoctorCommand.RunAsync(AnsiConsole.Console);
    });

    return command;
}
