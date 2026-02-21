using System.CommandLine;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Ouroboros.ApiHost.Extensions;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Commands.Options;
using Ouroboros.CLI.Commands.Handlers;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Services;
using Ouroboros.CLI.Hosting;

// ── Pre-parse --api-url and --serve before building the DI host ───────────────
// We need these values at host-construction time so we can swap service registrations.
string? apiUrlPreparse = null;
bool servePreparse = false;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--api-url" && i + 1 < args.Length)
        apiUrlPreparse = args[i + 1];
    if (args[i] == "--serve")
        servePreparse = true;
}

// Create the host builder
var hostBuilder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Register CLI services
        services.AddCliServices();

        // If --api-url was provided, redirect IAskService and IPipelineService
        // to the remote Ouroboros Web API (upstream provider mode).
        if (!string.IsNullOrWhiteSpace(apiUrlPreparse))
            services.AddUpstreamApiProvider(apiUrlPreparse);

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

// ── --serve: start an embedded Ouroboros API server alongside the CLI ─────────
Task? serveTask = null;
CancellationTokenSource? serveCts = null;
if (servePreparse)
{
    serveCts = new CancellationTokenSource();
    serveTask = Task.Run(async () =>
    {
        try
        {
            var webBuilder = WebApplication.CreateBuilder(args);
            webBuilder.Services.AddOuroborosWebApi();
            var webApp = webBuilder.Build();
            webApp.UseOuroborosWebApi();
            webApp.MapOuroborosApiEndpoints();
            AnsiConsole.MarkupLine("[cyan]Ouroboros API server starting (co-hosted with CLI)…[/]");
            await webApp.RunAsync(serveCts.Token);
        }
        catch (OperationCanceledException)
        {
            // expected on shutdown
        }
    }, serveCts.Token);
}

// Create the root command with System.CommandLine
var rootCommand = new RootCommand("Ouroboros CLI - Advanced AI Assistant System");

// Add --version option
var versionOption = new System.CommandLine.Option<bool>("--version");
versionOption.Description = "Show version information";
rootCommand.Add(versionOption);

// --serve: embed the Ouroboros API server in-process alongside the CLI
var serveOption = new System.CommandLine.Option<bool>("--serve");
serveOption.Description = "Co-host the Ouroboros Web API server inside this CLI process (accessible at http://localhost:5000 by default)";
serveOption.DefaultValueFactory = _ => false;
serveOption.Recursive = true;
rootCommand.Add(serveOption);

// --api-url: use a remote (or co-hosted) Ouroboros API as upstream provider
var apiUrlOption = new System.CommandLine.Option<string?>("--api-url");
apiUrlOption.Description = "Base URL of a running Ouroboros Web API to use as upstream provider (e.g. http://localhost:5000). Overrides local pipeline execution.";
apiUrlOption.DefaultValueFactory = _ => null;
apiUrlOption.Recursive = true;
rootCommand.Add(apiUrlOption);
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
    Console.Out.Write(parseResult.GetResult(rootCommand)!.Command.ToString());
    return Task.CompletedTask;
});

// Add global voice option (Recursive = true makes it propagate to all subcommands)
var voiceOption = new System.CommandLine.Option<bool>("--voice");
voiceOption.Description = "Enable voice interaction mode";
voiceOption.DefaultValueFactory = _ => false;
voiceOption.Recursive = true;
rootCommand.Add(voiceOption);

// Add subcommands
rootCommand.Add(CreateAskCommand(host, voiceOption));
rootCommand.Add(CreatePipelineCommand(host, voiceOption));
rootCommand.Add(CreateOuroborosCommand(host, voiceOption));
rootCommand.Add(CreateSkillsCommand(host, voiceOption));
rootCommand.Add(CreateOrchestratorCommand(host, voiceOption));
rootCommand.Add(CreateCognitivePhysicsCommand(host, voiceOption));
rootCommand.Add(CreateDoctorCommand());
rootCommand.Add(CreateChatCommand(host));
rootCommand.Add(CreateInteractiveCommand(host));
rootCommand.Add(CreateQualityCommand(host));

// Immersive persona mode and ambient room presence
rootCommand.Add(CreateImmersiveCommand(host, voiceOption));
rootCommand.Add(CreateRoomCommand());

// Add a special 'serve' subcommand for running API-only mode
rootCommand.Add(CreateServeCommand());

// Parse and invoke
int exitCode = await rootCommand.Parse(args).InvokeAsync();

// Shut down the co-hosted API server (if --serve was used)
if (serveCts is not null)
{
    await serveCts.CancelAsync();
    if (serveTask is not null)
        await serveTask.ConfigureAwait(false);
    serveCts.Dispose();
}

return exitCode;

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

    // Immersive and room as subcommands of ouroboros (also available at top level)
    command.Add(CreateImmersiveCommand(host, globalVoiceOption));
    command.Add(CreateRoomCommand());

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

static Command CreateChatCommand(IHost host)
{
    var command = new Command("chat", "Start an interactive chat session with the LLM");

    command.SetAction(async (parseResult, cancellationToken) =>
    {
        var askService = host.Services.GetRequiredService<IAskService>();
        await ChatCommand.RunAsync(askService, AnsiConsole.Console, cancellationToken);
    });

    return command;
}

static Command CreateInteractiveCommand(IHost host)
{
    var command = new Command("interactive", "Guided launcher — discover features through selection prompts");
    command.Aliases.Add("i");

    command.SetAction(async (parseResult, cancellationToken) =>
    {
        var askService = host.Services.GetRequiredService<IAskService>();
        var pipelineService = host.Services.GetRequiredService<IPipelineService>();
        await InteractiveCommand.RunAsync(askService, pipelineService, AnsiConsole.Console, cancellationToken);
    });

    return command;
}

static Command CreateQualityCommand(IHost host)
{
    var command = new Command("quality", "Render a rich product-quality and consistency dashboard");

    command.SetAction(async (parseResult, cancellationToken) =>
    {
        var handler = host.Services.GetRequiredService<QualityCommandHandler>();
        await handler.HandleAsync(cancellationToken);
    });

    return command;
}

/// <summary>
/// 'serve' subcommand — starts the Ouroboros Web API server in foreground mode
/// (no other CLI commands; blocks until Ctrl+C). Equivalent to running the
/// standalone Ouroboros.WebApi project but embedded in the CLI binary.
/// </summary>
static Command CreateServeCommand()
{
    var urlOption = new System.CommandLine.Option<string>("--url");
    urlOption.Description = "URL(s) to listen on (default: http://localhost:5000)";
    urlOption.DefaultValueFactory = _ => "http://localhost:5000";

    var command = new Command("serve", "Start the Ouroboros Web API server in-process (co-hosted with CLI)");
    command.Add(urlOption);

    command.SetAction(async (parseResult, cancellationToken) =>
    {
        var url = parseResult.GetValue(urlOption) ?? "http://localhost:5000";

        var webBuilder = WebApplication.CreateBuilder([]);
        webBuilder.WebHost.UseUrls(url);
        webBuilder.Services.AddOuroborosWebApi();
        var webApp = webBuilder.Build();
        webApp.UseOuroborosWebApi();
        webApp.MapOuroborosApiEndpoints();

        AnsiConsole.MarkupLine($"[cyan]Ouroboros API server listening on[/] [link]{url}[/]");
        AnsiConsole.MarkupLine("[dim]Press Ctrl+C to stop.[/]");
        await webApp.RunAsync(cancellationToken);
    });

    return command;
}

/// <summary>
/// 'immersive' subcommand — runs the full ImmersiveMode.RunImmersiveAsync persona experience.
/// This is the immersive AI persona shell: consciousness, memory, voice, skills, tools, avatar.
/// Default when no other subcommand is specified.
/// </summary>
static Command CreateImmersiveCommand(IHost host, System.CommandLine.Option<bool> globalVoiceOption)
{
    var command = new Command("immersive", "Run Iaret as an immersive persona (consciousness + memory + voice + avatar)");

    // Core model options
    var personaOpt   = new System.CommandLine.Option<string>("--persona")   { DefaultValueFactory = _ => "Iaret" };
    var modelOpt     = new System.CommandLine.Option<string>("--model")     { DefaultValueFactory = _ => "llama3:latest" };
    var endpointOpt  = new System.CommandLine.Option<string>("--endpoint")  { DefaultValueFactory = _ => "http://localhost:11434" };
    var embedOpt     = new System.CommandLine.Option<string>("--embed-model") { DefaultValueFactory = _ => "nomic-embed-text" };
    var qdrantOpt    = new System.CommandLine.Option<string>("--qdrant")    { DefaultValueFactory = _ => "http://localhost:6334" };
    var voiceOpt     = new System.CommandLine.Option<bool>("--voice-mode")  { DefaultValueFactory = _ => false };
    var localTtsOpt  = new System.CommandLine.Option<bool>("--local-tts")   { DefaultValueFactory = _ => false };
    var avatarOpt    = new System.CommandLine.Option<bool>("--avatar")      { DefaultValueFactory = _ => true };
    var avatarPortOpt = new System.CommandLine.Option<int>("--avatar-port") { DefaultValueFactory = _ => 9471 };
    var roomModeOpt  = new System.CommandLine.Option<bool>("--room-mode")   { DefaultValueFactory = _ => false };

    command.Add(personaOpt); command.Add(modelOpt); command.Add(endpointOpt);
    command.Add(embedOpt); command.Add(qdrantOpt); command.Add(voiceOpt);
    command.Add(localTtsOpt); command.Add(avatarOpt); command.Add(avatarPortOpt);
    command.Add(roomModeOpt);

    command.SetAction(async (parseResult, cancellationToken) =>
    {
        var opts = new Ouroboros.Options.ImmersiveCommandVoiceOptions
        {
            Persona       = parseResult.GetValue(personaOpt) ?? "Iaret",
            Model         = parseResult.GetValue(modelOpt) ?? "llama3:latest",
            Endpoint      = parseResult.GetValue(endpointOpt) ?? "http://localhost:11434",
            EmbedModel    = parseResult.GetValue(embedOpt) ?? "nomic-embed-text",
            QdrantEndpoint = parseResult.GetValue(qdrantOpt) ?? "http://localhost:6334",
            Voice         = parseResult.GetValue(voiceOpt) || parseResult.GetValue(globalVoiceOption),
            LocalTts      = parseResult.GetValue(localTtsOpt),
            Avatar        = parseResult.GetValue(avatarOpt),
            AvatarPort    = parseResult.GetValue(avatarPortOpt),
            RoomMode      = parseResult.GetValue(roomModeOpt),
        };
        await Ouroboros.CLI.Commands.ImmersiveMode.RunImmersiveAsync(opts, cancellationToken);
    });

    return command;
}

/// <summary>
/// 'room' subcommand — starts Iaret as ambient room presence.
/// Listens to the microphone, identifies speakers, and interjects with ethics + CogPhysics + Phi gating.
/// </summary>
static Command CreateRoomCommand()
{
    var opts = new Ouroboros.CLI.Commands.Options.RoomCommandOptions();
    var command = new Command("room", "Run Iaret as ambient room presence (ambient listening + ethics-gated interjections)");
    opts.AddToCommand(command);

    command.SetAction(async (parseResult, cancellationToken) =>
    {
        await Ouroboros.CLI.Commands.RoomMode.RunAsync(parseResult, opts, cancellationToken);
    });

    return command;
}
