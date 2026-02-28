using Microsoft.Extensions.Logging;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Services;

namespace Ouroboros.CLI.Commands.Handlers;

/// <summary>
/// Handler for the orchestrator command.
/// </summary>
public sealed class OrchestratorCommandHandler
{
    private readonly IOrchestratorService _orchestratorService;
    private readonly ISpectreConsoleService _console;
    private readonly IVoiceIntegrationService _voiceService;
    private readonly ILogger<OrchestratorCommandHandler> _logger;

    public OrchestratorCommandHandler(
        IOrchestratorService orchestratorService,
        ISpectreConsoleService console,
        IVoiceIntegrationService voiceService,
        ILogger<OrchestratorCommandHandler> logger)
    {
        _orchestratorService = orchestratorService;
        _console = console;
        _voiceService = voiceService;
        _logger = logger;
    }

    public async Task<int> HandleAsync(
        string goal,
        bool useVoice,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (useVoice)
            {
                await _voiceService.HandleVoiceCommandAsync(
                    "orchestrator", ["--goal", goal], cancellationToken);
                return 0;
            }

            await _console.Status().StartAsync("Orchestrating models...", async ctx =>
            {
                var result = await _orchestratorService.OrchestrateAsync(goal);
                ctx.Status = "Done";
                _console.MarkupLine($"[green]Result:[/] {result}");
            });

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Error executing orchestrator command");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            _logger.LogError(ex, "Error executing orchestrator command");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}
