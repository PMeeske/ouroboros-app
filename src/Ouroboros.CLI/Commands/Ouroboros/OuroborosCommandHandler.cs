using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ouroboros.CLI.Abstractions;
using Ouroboros.CLI.Commands.Options;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Services;

namespace Ouroboros.CLI.Commands.Handlers;

/// <summary>
/// Handler for the ouroboros agent command. Delegates config binding to
/// <see cref="OuroborosCommandOptions.BindConfig"/> and agent lifecycle to
/// <see cref="IOuroborosAgentService"/>.
/// </summary>
public sealed class OuroborosCommandHandler : ICommandHandler<OuroborosConfig>
{
    private readonly IOuroborosAgentService _agentService;
    private readonly ISpectreConsoleService _console;
    private readonly ILogger<OuroborosCommandHandler> _logger;
    private readonly IConfiguration _configuration;

    public OuroborosCommandHandler(
        IOuroborosAgentService agentService,
        ISpectreConsoleService console,
        ILogger<OuroborosCommandHandler> logger,
        IConfiguration configuration)
    {
        _agentService = agentService;
        _console = console;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<int> HandleAsync(
        OuroborosConfig config,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve OpenClaw token from user-secrets / appsettings if not provided via CLI or env
            if (config.EnableOpenClaw && string.IsNullOrEmpty(config.OpenClawToken))
            {
                var secretToken = _configuration["OpenClaw:Token"];
                if (!string.IsNullOrEmpty(secretToken))
                    config = config with { OpenClawToken = secretToken };
            }

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
