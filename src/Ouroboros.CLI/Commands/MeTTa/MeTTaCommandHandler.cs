using Microsoft.Extensions.Logging;
using Ouroboros.CLI.Abstractions;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Services;

namespace Ouroboros.CLI.Commands.Handlers;

/// <summary>
/// Handler for the <c>metta</c> command. Delegates to <see cref="IMeTTaService"/>.
/// Follows the same pattern as <see cref="ImmersiveCommandHandler"/>.
/// </summary>
public sealed class MeTTaCommandHandler : ICommandHandler<MeTTaConfig>
{
    private readonly IMeTTaService _mettaService;
    private readonly ISpectreConsoleService _console;
    private readonly ILogger<MeTTaCommandHandler> _logger;

    public MeTTaCommandHandler(
        IMeTTaService mettaService,
        ISpectreConsoleService console,
        ILogger<MeTTaCommandHandler> logger)
    {
        _mettaService = mettaService;
        _console = console;
        _logger = logger;
    }

    public async Task<int> HandleAsync(MeTTaConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            await _mettaService.RunAsync(config, cancellationToken);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "metta command failed");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}
