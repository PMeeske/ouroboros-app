using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ouroboros.CLI.Commands.Options;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Services;

namespace Ouroboros.CLI.Commands.Handlers;

/// <summary>
/// Handler for the ask command using System.CommandLine 2.0.3 GA
/// </summary>
public class AskCommandHandler
{
    private readonly IAskService _askService;
    private readonly ISpectreConsoleService _console;
    private readonly IVoiceIntegrationService _voiceService;
    private readonly ILogger<AskCommandHandler> _logger;

    public AskCommandHandler(
        IAskService askService,
        ISpectreConsoleService console,
        IVoiceIntegrationService voiceService,
        ILogger<AskCommandHandler> logger)
    {
        _askService = askService;
        _console = console;
        _voiceService = voiceService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the ask command execution
    /// </summary>
    public async Task<int> HandleAsync(
        string question,
        bool rag,
        string? culture,
        string model,
        string embed,
        int topK,
        double temperature,
        int maxTokens,
        int timeoutSeconds,
        bool stream,
        string router,
        string? coderModel,
        string? summarizeModel,
        string? reasonModel,
        string? generalModel,
        bool debug,
        bool agent,
        string agentMode,
        int agentMaxSteps,
        bool strictModel,
        bool jsonTools,
        string? endpoint,
        string? apiKey,
        string? endpointType,
        string decompose,
        string collective,
        string? masterModel,
        string electionStrategy,
        bool showSubgoals,
        bool parallelSubgoals,
        bool voiceOnly,
        bool localTts,
        bool voiceLoop,
        bool useVoice,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (useVoice)
            {
                await _voiceService.HandleVoiceCommandAsync(
                    "ask",
                    ["--question", question, "--rag", rag.ToString()],
                    cancellationToken);
                return 0;
            }

            await _console.Status().StartAsync("Processing question...", async ctx =>
            {
                var result = await _askService.AskAsync(question, rag);
                ctx.Status = "Done";
                _console.MarkupLine($"[green]Answer:[/] {result}");
            });

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing ask command");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}

/// <summary>
/// Extension methods for registering the ask command handler
/// </summary>
public static class AskCommandHandlerExtensions
{
    /// <summary>
    /// Registers the ask command handler with the service collection
    /// </summary>
    public static IServiceCollection AddAskCommandHandler(this IServiceCollection services)
    {
        services.AddScoped<AskCommandHandler>();
        return services;
    }

    /// <summary>
    /// Configures the ask command with its handler using System.CommandLine 2.0.3 API
    /// </summary>
    public static Command ConfigureAskCommand(
        this Command command,
        IHost host,
        AskCommandOptions options,
        System.CommandLine.Option<bool> globalVoiceOption)
    {
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var handler = host.Services.GetRequiredService<AskCommandHandler>();

            await handler.HandleAsync(
                question: parseResult.GetValue(options.QuestionOption) ?? string.Empty,
                rag: parseResult.GetValue(options.RagOption),
                culture: parseResult.GetValue(options.CultureOption),
                model: parseResult.GetValue(options.ModelOption) ?? "ministral-3:latest",
                embed: parseResult.GetValue(options.EmbedOption) ?? "nomic-embed-text",
                topK: parseResult.GetValue(options.TopKOption),
                temperature: parseResult.GetValue(options.TemperatureOption),
                maxTokens: parseResult.GetValue(options.MaxTokensOption),
                timeoutSeconds: parseResult.GetValue(options.TimeoutSecondsOption),
                stream: parseResult.GetValue(options.StreamOption),
                router: parseResult.GetValue(options.RouterOption) ?? "off",
                coderModel: parseResult.GetValue(options.CoderModelOption),
                summarizeModel: parseResult.GetValue(options.SummarizeModelOption),
                reasonModel: parseResult.GetValue(options.ReasonModelOption),
                generalModel: parseResult.GetValue(options.GeneralModelOption),
                debug: parseResult.GetValue(options.DebugOption),
                agent: parseResult.GetValue(options.AgentOption),
                agentMode: parseResult.GetValue(options.AgentModeOption) ?? "lc",
                agentMaxSteps: parseResult.GetValue(options.AgentMaxStepsOption),
                strictModel: parseResult.GetValue(options.StrictModelOption),
                jsonTools: parseResult.GetValue(options.JsonToolsOption),
                endpoint: parseResult.GetValue(options.EndpointOption),
                apiKey: parseResult.GetValue(options.ApiKeyOption),
                endpointType: parseResult.GetValue(options.EndpointTypeOption),
                decompose: parseResult.GetValue(options.DecomposeOption) ?? "off",
                collective: parseResult.GetValue(options.CollectiveOption) ?? "off",
                masterModel: parseResult.GetValue(options.MasterModelOption),
                electionStrategy: parseResult.GetValue(options.ElectionStrategyOption) ?? "weighted",
                showSubgoals: parseResult.GetValue(options.ShowSubgoalsOption),
                parallelSubgoals: parseResult.GetValue(options.ParallelSubgoalsOption),
                voiceOnly: parseResult.GetValue(options.VoiceOnlyOption),
                localTts: parseResult.GetValue(options.LocalTtsOption),
                voiceLoop: parseResult.GetValue(options.VoiceLoopOption),
                useVoice: parseResult.GetValue(globalVoiceOption),
                cancellationToken);
        });

        return command;
    }
}
