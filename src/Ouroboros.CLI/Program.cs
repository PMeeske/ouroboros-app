#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==============================
// Modern CLI entry with System.CommandLine + Microsoft.Extensions.Hosting
// ==============================

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Ouroboros.CLI.Commands;
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
        
        // Register existing business logic services
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

// Add subcommands
rootCommand.AddCommand(CreateAskCommand());
rootCommand.AddCommand(CreatePipelineCommand());
rootCommand.AddCommand(CreateOuroborosCommand());
rootCommand.AddCommand(CreateSkillsCommand());
rootCommand.AddCommand(CreateOrchestratorCommand());
// Add other commands...

// Add global voice option
var voiceOption = new Option<bool>(
    name: "--voice",
    description: "Enable voice interaction mode",
    getDefaultValue: () => false);

rootCommand.AddGlobalOption(voiceOption);

// Parse and invoke
return await rootCommand.InvokeAsync(args);

// Command creation methods
static Command CreateAskCommand()
{
    var command = new Command("ask", "Ask the LLM a question");
    
    var questionOption = new Option<string>(
        name: "--question",
        description: "The question to ask",
        getDefaultValue: () => string.Empty);
    
    var ragOption = new Option<bool>(
        name: "--rag",
        description: "Enable RAG context",
        getDefaultValue: () => false);
    
    command.AddOption(questionOption);
    command.AddOption(ragOption);
    
    command.SetHandler(async (context) =>
    {
        var host = context.GetHost();
        var askService = host.Services.GetRequiredService<IAskService>();
        var console = host.Services.GetRequiredService<ISpectreConsoleService>();
        var voiceService = host.Services.GetRequiredService<IVoiceIntegrationService>();
        
        var question = context.ParseResult.GetValueForOption(questionOption);
        var useRag = context.ParseResult.GetValueForOption(ragOption);
        var useVoice = context.ParseResult.GetValueForOption(voiceOption);
        
        if (useVoice)
        {
            await voiceService.HandleVoiceCommandAsync("ask", new[] { "--question", question, "--rag", useRag.ToString() });
            return;
        }
        
        await console.Status().StartAsync("Processing question...", async ctx =>
        {
            var result = await askService.AskAsync(question, useRag);
            ctx.Status = "Done";
            console.MarkupLine($"[green]Answer:[/] {result}");
        });
    });
    
    return command;
}

static Command CreatePipelineCommand()
{
    var command = new Command("pipeline", "Execute a DSL pipeline");
    
    var dslOption = new Option<string>(
        name: "--dsl",
        description: "The pipeline DSL to execute",
        getDefaultValue: () => string.Empty);
    
    command.AddOption(dslOption);
    
    command.SetHandler(async (context) =>
    {
        var host = context.GetHost();
        var pipelineService = host.Services.GetRequiredService<IPipelineService>();
        var console = host.Services.GetRequiredService<ISpectreConsoleService>();
        var voiceService = host.Services.GetRequiredService<IVoiceIntegrationService>();
        
        var dsl = context.ParseResult.GetValueForOption(dslOption);
        var useVoice = context.ParseResult.GetValueForOption(voiceOption);
        
        if (useVoice)
        {
            await voiceService.HandleVoiceCommandAsync("pipeline", new[] { "--dsl", dsl });
            return;
        }
        
        await console.Status().StartAsync("Executing pipeline...", async ctx =>
        {
            var result = await pipelineService.ExecutePipelineAsync(dsl);
            ctx.Status = "Done";
            console.MarkupLine($"[green]Result:[/] {result}");
        });
    });
    
    return command;
}

static Command CreateOuroborosCommand()
{
    var command = new Command("ouroboros", "Run Ouroboros agent mode");
    
    var personaOption = new Option<string>(
        name: "--persona",
        description: "Agent persona",
        getDefaultValue: () => "Ouroboros");
    
    command.AddOption(personaOption);
    
    command.SetHandler(async (context) =>
    {
        var host = context.GetHost();
        var agentService = host.Services.GetRequiredService<IOuroborosAgentService>();
        var console = host.Services.GetRequiredService<ISpectreConsoleService>();
        var voiceService = host.Services.GetRequiredService<IVoiceIntegrationService>();
        
        var persona = context.ParseResult.GetValueForOption(personaOption);
        var useVoice = context.ParseResult.GetValueForOption(voiceOption);
        
        if (useVoice)
        {
            await voiceService.HandleVoiceCommandAsync("ouroboros", new[] { "--persona", persona });
            return;
        }
        
        await console.Status().StartAsync("Initializing agent...", async ctx =>
        {
            await agentService.RunAgentAsync(persona);
            ctx.Status = "Agent running";
        });
    });
    
    return command;
}

static Command CreateSkillsCommand()
{
    var command = new Command("skills", "Manage research skills");
    
    var listOption = new Option<bool>(
        name: "--list",
        description: "List all skills",
        getDefaultValue: () => false);
    
    var fetchOption = new Option<string>(
        name: "--fetch",
        description: "Fetch research and extract skills");
    
    command.AddOption(listOption);
    command.AddOption(fetchOption);
    
    command.SetHandler(async (context) =>
    {
        var host = context.GetHost();
        var skillsService = host.Services.GetRequiredService<ISkillsService>();
        var console = host.Services.GetRequiredService<ISpectreConsoleService>();
        var voiceService = host.Services.GetRequiredService<IVoiceIntegrationService>();
        
        var list = context.ParseResult.GetValueForOption(listOption);
        var fetch = context.ParseResult.GetValueForOption(fetchOption);
        var useVoice = context.ParseResult.GetValueForOption(voiceOption);
        
        if (useVoice)
        {
            var args = new List<string>();
            if (list) args.AddRange(new[] { "--list", "true" });
            if (!string.IsNullOrEmpty(fetch)) args.AddRange(new[] { "--fetch", fetch });
            await voiceService.HandleVoiceCommandAsync("skills", args.ToArray());
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

static Command CreateOrchestratorCommand()
{
    var command = new Command("orchestrator", "Run multi-model orchestrator");
    
    var goalOption = new Option<string>(
        name: "--goal",
        description: "Goal for the orchestrator",
        getDefaultValue: () => string.Empty);
    
    command.AddOption(goalOption);
    
    command.SetHandler(async (context) =>
    {
        var host = context.GetHost();
        var orchestratorService = host.Services.GetRequiredService<IOrchestratorService>();
        var console = host.Services.GetRequiredService<ISpectreConsoleService>();
        var voiceService = host.Services.GetRequiredService<IVoiceIntegrationService>();
        
        var goal = context.ParseResult.GetValueForOption(goalOption);
        var useVoice = context.ParseResult.GetValueForOption(voiceOption);
        
        if (useVoice)
        {
            await voiceService.HandleVoiceCommandAsync("orchestrator", new[] { "--goal", goal });
            return;
        }
        
        await console.Status().StartAsync("Orchestrating models...", async ctx =>
        {
            var result = await orchestratorService.OrchestrateAsync(goal);
            ctx.Status = "Done";
            console.MarkupLine($"[green]Result:[/] {result}");
        });
    });
    
    return command;
}

