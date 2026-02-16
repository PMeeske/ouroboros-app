using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ouroboros.CLI.Commands.Options;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Services;

namespace Ouroboros.CLI.Commands.Handlers;

/// <summary>
/// Handler for the ask command.
/// </summary>
public sealed class AskCommandHandler
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

    public async Task<int> HandleAsync(
        string question,
        bool rag,
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
/// Extension methods for registering the ask command handler and configuring the ask command.
/// </summary>
public static class AskCommandHandlerExtensions
{
    public static IServiceCollection AddAskCommandHandler(this IServiceCollection services)
    {
        services.AddScoped<AskCommandHandler>();
        return services;
    }

    public static Command ConfigureAskCommand(
        this Command command,
        IHost host,
        AskCommandOptions options,
        Option<bool> globalVoiceOption)
    {
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var handler = host.Services.GetRequiredService<AskCommandHandler>();

            await handler.HandleAsync(
                question: parseResult.GetValue(options.QuestionOption) ?? string.Empty,
                rag: parseResult.GetValue(options.RagOption),
                useVoice: parseResult.GetValue(globalVoiceOption),
                cancellationToken);
        });

        return command;
    }
}
