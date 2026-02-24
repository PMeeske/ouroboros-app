using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ouroboros.CLI.Abstractions;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Services;

namespace Ouroboros.CLI.Commands.Handlers;

/// <summary>
/// Handler for the immersive persona command. Delegates config binding to
/// <see cref="Options.ImmersiveCommandOptions.BindConfig"/> and session lifecycle to
/// <see cref="IImmersiveModeService"/>.
/// Follows the same pattern as <see cref="OuroborosCommandHandler"/>.
/// </summary>
public sealed class ImmersiveCommandHandler : ICommandHandler<ImmersiveConfig>
{
    private readonly IImmersiveModeService _immersiveService;
    private readonly ISpectreConsoleService _console;
    private readonly ILogger<ImmersiveCommandHandler> _logger;
    private readonly IConfiguration _configuration;

    public ImmersiveCommandHandler(
        IImmersiveModeService immersiveService,
        ISpectreConsoleService console,
        ILogger<ImmersiveCommandHandler> logger,
        IConfiguration configuration)
    {
        _immersiveService = immersiveService;
        _console = console;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<int> HandleAsync(
        ImmersiveConfig config,
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

            await _immersiveService.RunAsync(config, cancellationToken);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running immersive mode");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}
