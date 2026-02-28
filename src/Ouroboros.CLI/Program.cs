using System.CommandLine;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Ouroboros.ApiHost.Extensions;
using Ouroboros.Application.Configuration;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Commands.Options;
using Ouroboros.CLI.Commands.Handlers;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Services;
using Ouroboros.CLI.Hosting;

// ── Global exception handler ─────────────────────────────────────────────────
// Catches unhandled exceptions during host construction, DI resolution,
// and command execution to display friendly diagnostics instead of raw
// stack traces.  Particularly useful when submodules are out-of-sync and
// assemblies or configuration files are missing.
bool verbose = args.Contains("--verbose")
    || Environment.GetEnvironmentVariable("OUROBOROS_VERBOSE") == "1";

try
{

// ── Pre-parse --api-url and --serve before building the DI host ───────────────
// We need these values at host-construction time so we can swap service registrations.
// Handles: --api-url VALUE, --api-url=VALUE, --serve
string? apiUrlPreparse = null;
bool servePreparse = false;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--api-url" && i + 1 < args.Length)
        apiUrlPreparse = args[i + 1];
    else if (args[i].StartsWith("--api-url=", StringComparison.Ordinal))
        apiUrlPreparse = args[i]["--api-url=".Length..];
    if (args[i] == "--serve")
        servePreparse = true;
}

// Create the host builder — unified bootstrapping via AddCliHost() which
// calls AddOuroborosEngine() (shared with WebApi) then layers CLI-specific services.
var hostBuilder = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((_, config) =>
    {
        // Load user secrets unconditionally so local dev keys (OpenClaw token, API keys,
        // etc.) are available regardless of DOTNET_ENVIRONMENT.
        config.AddUserSecrets(System.Reflection.Assembly.GetEntryAssembly()!, optional: true);
    })
    .ConfigureServices((context, services) =>
    {
        // Engine + foundational deps (cognitive physics, self-model, health checks,
        // Qdrant cross-cutting infrastructure) plus CLI services, command handlers,
        // infrastructure, and business logic.
        services.AddCliHost(context.Configuration);

        // If --api-url was provided, redirect IAskService and IPipelineService
        // to the remote Ouroboros Web API (upstream provider mode).
        if (!string.IsNullOrWhiteSpace(apiUrlPreparse))
            services.AddUpstreamApiProvider(apiUrlPreparse);
    })
    .ConfigureLogging((context, logging) =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Warning);
    })
    // Agent-dependent MediatR handlers (OuroborosAgent, subsystems) live in the
    // child container built by AgentBootstrapper.CreateAgentWithDIAsync, not in
    // the host container.  The assembly-wide MediatR scan registers them here
    // too, but they are never resolved from the host — skip build-time validation
    // so these unused registrations don't block startup.
    .UseDefaultServiceProvider(options => options.ValidateOnBuild = false);

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
            webBuilder.Services.AddOuroborosWebApi(webBuilder.Configuration);
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
serveOption.Description = $"Co-host the Ouroboros Web API server inside this CLI process (accessible at {DefaultEndpoints.OuroborosApi} by default)";
serveOption.DefaultValueFactory = _ => false;
serveOption.Recursive = true;
rootCommand.Add(serveOption);

// --api-url: use a remote (or co-hosted) Ouroboros API as upstream provider
var apiUrlOption = new System.CommandLine.Option<string?>("--api-url");
apiUrlOption.Description = $"Base URL of a running Ouroboros Web API to use as upstream provider (e.g. {DefaultEndpoints.OuroborosApi}). Overrides local pipeline execution.";
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
rootCommand.Add(CreateCognitivePhysicsCommand(host));
rootCommand.Add(CreateDoctorCommand());
rootCommand.Add(CreateChatCommand(host));
rootCommand.Add(CreateInteractiveCommand(host));
rootCommand.Add(CreateQualityCommand(host));
rootCommand.Add(CreateMeTTaCommand(host, voiceOption));

// Immersive persona mode and ambient room presence
rootCommand.Add(CreateImmersiveCommand(host, voiceOption));
rootCommand.Add(CreateRoomCommand(host));

// Claude Code diagnostic tools
rootCommand.Add(CreateClaudeCommand(host));

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

}
catch (Exception ex)
{
    // Peel off TargetInvocationException / TypeInitializationException wrappers
    Exception root = ex;
    while (root is TargetInvocationException or TypeInitializationException && root.InnerException is not null)
        root = root.InnerException;

    // Classify and render a user-friendly message
    if (root is FileNotFoundException fnf)
    {
        AnsiConsole.MarkupLine("[red bold]Missing assembly or file[/]");
        AnsiConsole.MarkupLine($"[red]{Markup.Escape(fnf.FileName ?? fnf.Message)}[/]");
        AnsiConsole.MarkupLine("[yellow]This usually means a submodule is out of sync. Try:[/]");
        AnsiConsole.MarkupLine("[dim]  git submodule update --init --recursive[/]");
        AnsiConsole.MarkupLine("[dim]  dotnet restore[/]");
    }
    else if (root is FileLoadException fle)
    {
        AnsiConsole.MarkupLine("[red bold]Assembly version mismatch[/]");
        AnsiConsole.MarkupLine($"[red]{Markup.Escape(fle.FileName ?? fle.Message)}[/]");
        AnsiConsole.MarkupLine("[yellow]Rebuild after updating submodules:[/]");
        AnsiConsole.MarkupLine("[dim]  git submodule update --init --recursive[/]");
        AnsiConsole.MarkupLine("[dim]  dotnet build[/]");
    }
    else if (root is InvalidOperationException ioe
             && ioe.Message.Contains("Unable to resolve service", StringComparison.OrdinalIgnoreCase))
    {
        AnsiConsole.MarkupLine("[red bold]Dependency injection failure[/]");
        AnsiConsole.MarkupLine($"[red]{Markup.Escape(ioe.Message)}[/]");
        AnsiConsole.MarkupLine("[yellow]A required service could not be resolved. Check that all projects built successfully.[/]");
    }
    else if (root is DllNotFoundException dll)
    {
        AnsiConsole.MarkupLine("[red bold]Missing native library[/]");
        AnsiConsole.MarkupLine($"[red]{Markup.Escape(dll.Message)}[/]");
        AnsiConsole.MarkupLine("[yellow]Run 'ouroboros doctor' to check for missing dependencies.[/]");
    }
    else
    {
        AnsiConsole.MarkupLine("[red bold]Unexpected error[/]");
        AnsiConsole.MarkupLine($"[red]{Markup.Escape(root.GetType().Name)}: {Markup.Escape(root.Message)}[/]");
    }

    // Show full stack trace under --verbose or OUROBOROS_VERBOSE=1
    if (verbose)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.WriteException(ex, ExceptionFormats.ShortenPaths | ExceptionFormats.ShortenMethods);
    }
    else
    {
        AnsiConsole.MarkupLine("[dim]Run with --verbose or OUROBOROS_VERBOSE=1 for full stack trace.[/]");
    }

    return 1;
}

// ────────────────────────────────────────────────────────
// Command creation methods
// ────────────────────────────────────────────────────────

static Command CreateAskCommand(IHost host, System.CommandLine.Option<bool> globalVoiceOption)
{
    var options = new AskCommandOptions();
    var command = new Command("ask", "Ask the LLM a question");
    command.Aliases.Add("a");

    // Add all options using the helper
    options.AddToCommand(command);

    // Configure handler via extension method
    return command.ConfigureAskCommand(host, options, globalVoiceOption);
}

static Command CreatePipelineCommand(IHost host, System.CommandLine.Option<bool> globalVoiceOption)
{
    var options = new PipelineCommandOptions();
    var command = new Command("pipeline", "Execute a DSL pipeline");
    command.Aliases.Add("p");
    options.AddToCommand(command);
    return command.ConfigurePipelineCommand(host, options, globalVoiceOption);
}

static Command CreateOuroborosCommand(IHost host, System.CommandLine.Option<bool> globalVoiceOption)
{
    var options = new OuroborosCommandOptions();
    var command = new Command("ouroboros", "Run Ouroboros agent mode");
    command.Aliases.Add("o");

    // Add all options using the helper
    options.AddToCommand(command);

    // Immersive and room as subcommands of ouroboros (also available at top level)
    command.Add(CreateImmersiveCommand(host, globalVoiceOption));
    command.Add(CreateRoomCommand(host));

    // Configure handler via extension method (mirrors CreateAskCommand pattern)
    return command.ConfigureOuroborosCommand(host, options, globalVoiceOption);
}

static Command CreateSkillsCommand(IHost host, System.CommandLine.Option<bool> globalVoiceOption)
{
    var options = new SkillsCommandOptions();
    var command = new Command("skills", "Manage research skills");
    options.AddToCommand(command);
    return command.ConfigureSkillsCommand(host, options, globalVoiceOption);
}

static Command CreateOrchestratorCommand(IHost host, System.CommandLine.Option<bool> globalVoiceOption)
{
    var options = new OrchestratorCommandOptions();
    var command = new Command("orchestrator", "Run multi-model orchestrator");
    options.AddToCommand(command);
    return command.ConfigureOrchestratorCommand(host, options, globalVoiceOption);
}

static Command CreateCognitivePhysicsCommand(IHost host)
{
    var options = new CognitivePhysicsCommandOptions();
    var command = new Command("cognitive-physics", "Execute Cognitive Physics Engine operations (ZeroShift, superposition, chaos, evolution)");
    options.AddToCommand(command);
    return command.ConfigureCognitivePhysicsCommand(host, options);
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

static Command CreateMeTTaCommand(IHost host, System.CommandLine.Option<bool> globalVoiceOption)
{
    var options = new MeTTaCommandOptions();
    var command = new Command("metta", "Run MeTTa orchestrator with symbolic reasoning");
    options.AddToCommand(command);
    return command.ConfigureMeTTaCommand(host, options, globalVoiceOption);
}

/// <summary>
/// 'serve' subcommand — starts the Ouroboros Web API server in foreground mode
/// (no other CLI commands; blocks until Ctrl+C). Equivalent to running the
/// standalone Ouroboros.WebApi project but embedded in the CLI binary.
/// </summary>
static Command CreateServeCommand()
{
    var urlOption = new System.CommandLine.Option<string>("--url");
    urlOption.Description = "URL(s) to listen on (default: " + DefaultEndpoints.OuroborosApi + ")";
    urlOption.DefaultValueFactory = _ => DefaultEndpoints.OuroborosApi;

    var command = new Command("serve", "Start the Ouroboros Web API server in-process (co-hosted with CLI)");
    command.Add(urlOption);

    command.SetAction(async (parseResult, cancellationToken) =>
    {
        var url = parseResult.GetValue(urlOption) ?? DefaultEndpoints.OuroborosApi;

        var webBuilder = WebApplication.CreateBuilder([]);
        webBuilder.WebHost.UseUrls(url);
        webBuilder.Services.AddOuroborosWebApi(webBuilder.Configuration);
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
/// 'immersive' subcommand — runs the full ImmersiveMode persona experience.
/// Uses the handler pattern: ImmersiveCommandOptions → ImmersiveConfig → ImmersiveCommandHandler → IImmersiveModeService.
/// </summary>
static Command CreateImmersiveCommand(IHost host, System.CommandLine.Option<bool> globalVoiceOption)
{
    var options = new ImmersiveCommandOptions();
    var command = new Command("immersive", "Run Iaret as an immersive persona (consciousness + memory + voice + avatar)");
    options.AddToCommand(command);
    return command.ConfigureImmersiveCommand(host, options, globalVoiceOption);
}

/// <summary>
/// 'room' subcommand — starts Iaret as ambient room presence.
/// Uses the handler pattern: RoomCommandOptions → RoomConfig → RoomCommandHandler → IRoomModeService.
/// </summary>
static Command CreateRoomCommand(IHost host)
{
    var options = new RoomCommandOptions();
    var command = new Command("room", "Run Iaret as ambient room presence (ambient listening + ethics-gated interjections)");
    options.AddToCommand(command);
    return command.ConfigureRoomCommand(host, options);
}

/// <summary>
/// 'claude' subcommand — Claude Code diagnostic tools: memory health, file integrity,
/// Qdrant state (local + cloud), and submodule sync verification.
/// </summary>
static Command CreateClaudeCommand(IHost host)
{
    var command = new Command("claude", "Claude Code diagnostic tools — check memory, files, Qdrant, and sync state");

    // ── claude check ──
    var checkCommand = new Command("check", "Run comprehensive diagnostics on Qdrant, CLAUDE.md, memory, and submodules");
    checkCommand.SetAction(async (parseResult, cancellationToken) =>
    {
        var handler = host.Services.GetRequiredService<ClaudeCheckCommandHandler>();
        await handler.HandleAsync(cancellationToken);
    });
    command.Add(checkCommand);

    // ── claude backup ──
    var backupOutputOption = new System.CommandLine.Option<string?>("--output", "-o")
    {
        Description = "Output directory for backup (default: ~/.claude/backups/<timestamp>)",
    };
    var backupCommand = new Command("backup", "Backup CLAUDE.md and MEMORY.md files with integrity manifest");
    backupCommand.Add(backupOutputOption);
    backupCommand.SetAction(async (parseResult, cancellationToken) =>
    {
        var output = parseResult.GetValue(backupOutputOption);
        await ClaudeBackupCommand.RunAsync(AnsiConsole.Console, output);
    });
    command.Add(backupCommand);

    // ── claude restore ──
    var restorePathArg = new Argument<string>("backup-path") { Description = "Path to backup directory to restore from" };
    var restoreCommand = new Command("restore", "Restore CLAUDE.md and MEMORY.md files from a backup");
    restoreCommand.Add(restorePathArg);
    restoreCommand.SetAction(async (parseResult, cancellationToken) =>
    {
        var path = parseResult.GetValue(restorePathArg);
        await ClaudeRestoreCommand.RunAsync(AnsiConsole.Console, path);
    });
    command.Add(restoreCommand);

    // Default action (no subcommand) = check
    command.SetAction(async (parseResult, cancellationToken) =>
    {
        var handler = host.Services.GetRequiredService<ClaudeCheckCommandHandler>();
        await handler.HandleAsync(cancellationToken);
    });

    return command;
}
