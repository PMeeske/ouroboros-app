using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ouroboros.CLI.Commands.Options;
using Ouroboros.CLI.Commands.Handlers;
using Ouroboros.CLI.Hosting;

// ── Build the host ─────────────────────────────────────────────────────
using var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((_, services) =>
    {
        services.AddCliServices();
        services.AddCommandHandlers();
        services.AddInfrastructureServices();
        services.AddCognitivePhysicsDefaults();
        services.AddExistingBusinessLogic();
    })
    .ConfigureLogging((_, logging) =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Warning);
    })
    .Build();

// ── Build the command tree ─────────────────────────────────────────────
var rootCommand = new RootCommand("Ouroboros CLI - Advanced AI Assistant System");

var voiceOption = new Option<bool>("--voice")
{
    Description = "Enable voice interaction mode",
    DefaultValueFactory = _ => false,
    Recursive = true
};
rootCommand.Add(voiceOption);

rootCommand.Add(CreateAskCommand(host, voiceOption));
rootCommand.Add(CreatePipelineCommand(host, voiceOption));
rootCommand.Add(CreateOuroborosCommand(host));
rootCommand.Add(CreateSkillsCommand(host, voiceOption));
rootCommand.Add(CreateOrchestratorCommand(host, voiceOption));
rootCommand.Add(CreateCognitivePhysicsCommand(host));

return await rootCommand.Parse(args).InvokeAsync();

// ── Command factories ──────────────────────────────────────────────────

static Command CreateAskCommand(IHost host, Option<bool> globalVoiceOption)
{
    var options = new AskCommandOptions();
    var command = new Command("ask", "Ask the LLM a question");
    options.AddToCommand(command);
    return command.ConfigureAskCommand(host, options, globalVoiceOption);
}

static Command CreatePipelineCommand(IHost host, Option<bool> globalVoiceOption)
{
    var options = new PipelineCommandOptions();
    var command = new Command("pipeline", "Execute a DSL pipeline");
    options.AddToCommand(command);

    command.SetAction(async (parseResult, cancellationToken) =>
    {
        var handler = host.Services.GetRequiredService<PipelineCommandHandler>();
        await handler.HandleAsync(
            dsl: parseResult.GetValue(options.DslOption) ?? "",
            useVoice: parseResult.GetValue(globalVoiceOption),
            cancellationToken);
    });

    return command;
}

static Command CreateOuroborosCommand(IHost host)
{
    var options = new OuroborosCommandOptions();
    var command = new Command("ouroboros", "Run Ouroboros agent mode");
    options.AddToCommand(command);

    command.SetAction(async (parseResult, cancellationToken) =>
    {
        var handler = host.Services.GetRequiredService<OuroborosCommandHandler>();
        var config = options.BindConfig(parseResult);
        await handler.HandleAsync(config, cancellationToken);
    });

    return command;
}

static Command CreateSkillsCommand(IHost host, Option<bool> globalVoiceOption)
{
    var options = new SkillsCommandOptions();
    var command = new Command("skills", "Manage research skills");
    options.AddToCommand(command);

    command.SetAction(async (parseResult, cancellationToken) =>
    {
        var handler = host.Services.GetRequiredService<SkillsCommandHandler>();
        await handler.HandleAsync(
            list: parseResult.GetValue(options.ListOption),
            fetch: parseResult.GetValue(options.FetchOption),
            useVoice: parseResult.GetValue(globalVoiceOption),
            cancellationToken);
    });

    return command;
}

static Command CreateOrchestratorCommand(IHost host, Option<bool> globalVoiceOption)
{
    var options = new OrchestratorCommandOptions();
    var command = new Command("orchestrator", "Run multi-model orchestrator");
    options.AddToCommand(command);

    command.SetAction(async (parseResult, cancellationToken) =>
    {
        var handler = host.Services.GetRequiredService<OrchestratorCommandHandler>();
        await handler.HandleAsync(
            goal: parseResult.GetValue(options.GoalOption) ?? "",
            useVoice: parseResult.GetValue(globalVoiceOption),
            cancellationToken);
    });

    return command;
}

static Command CreateCognitivePhysicsCommand(IHost host)
{
    var options = new CognitivePhysicsCommandOptions();
    var command = new Command("cognitive-physics",
        "Execute Cognitive Physics Engine operations (ZeroShift, superposition, chaos, evolution)");
    options.AddToCommand(command);

    command.SetAction(async (parseResult, cancellationToken) =>
    {
        var handler = host.Services.GetRequiredService<CognitivePhysicsCommandHandler>();
        await handler.HandleAsync(
            operation: parseResult.GetValue(options.OperationOption) ?? "shift",
            focus: parseResult.GetValue(options.FocusOption) ?? "general",
            target: parseResult.GetValue(options.TargetOption),
            targets: parseResult.GetValue(options.TargetsOption),
            resources: parseResult.GetValue(options.ResourcesOption),
            verbose: parseResult.GetValue(options.VerboseOption),
            cancellationToken);
    });

    return command;
}
