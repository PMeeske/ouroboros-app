using Microsoft.Extensions.Logging;
using Ouroboros.CLI.Commands.Options;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Services;

namespace Ouroboros.CLI.Commands.Handlers;

/// <summary>
/// Handler for the ouroboros agent command. Delegates config binding to
/// <see cref="OuroborosCommandOptions.BindConfig"/> and agent lifecycle to
/// <see cref="IOuroborosAgentService"/>.
/// </summary>
public sealed class OuroborosCommandHandler
{
    private readonly IOuroborosAgentService _agentService;
    private readonly ISpectreConsoleService _console;
    private readonly ILogger<OuroborosCommandHandler> _logger;

    public OuroborosCommandHandler(
        IOuroborosAgentService agentService,
        ISpectreConsoleService console,
        ILogger<OuroborosCommandHandler> logger)
    {
        _agentService = agentService;
        _console = console;
        _logger = logger;
    }

    public async Task<int> HandleAsync(
        OuroborosConfig config,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _agentService.RunAgentAsync(config, cancellationToken);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running Ouroboros agent");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}
