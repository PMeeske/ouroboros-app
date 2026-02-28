using Microsoft.Extensions.Logging;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Services;

namespace Ouroboros.CLI.Commands.Handlers;

/// <summary>
/// Handler for the pipeline command. Bridges System.CommandLine parsing to service execution.
/// </summary>
public sealed class PipelineCommandHandler
{
    private readonly IPipelineService _pipelineService;
    private readonly ISpectreConsoleService _console;
    private readonly IVoiceIntegrationService _voiceService;
    private readonly ILogger<PipelineCommandHandler> _logger;

    public PipelineCommandHandler(
        IPipelineService pipelineService,
        ISpectreConsoleService console,
        IVoiceIntegrationService voiceService,
        ILogger<PipelineCommandHandler> logger)
    {
        _pipelineService = pipelineService;
        _console = console;
        _voiceService = voiceService;
        _logger = logger;
    }

    public async Task<int> HandleAsync(
        string dsl,
        bool useVoice,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (useVoice)
            {
                await _voiceService.HandleVoiceCommandAsync(
                    "pipeline", ["--dsl", dsl], cancellationToken);
                return 0;
            }

            await _console.Status().StartAsync("Executing pipeline...", async ctx =>
            {
                var result = await _pipelineService.ExecutePipelineAsync(dsl);
                ctx.Status = "Done";
                _console.MarkupLine($"[green]Result:[/] {result}");
            });

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Error executing pipeline command");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            _logger.LogError(ex, "Error executing pipeline command");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}
