using Microsoft.Extensions.Logging;
using Ouroboros.Options;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// DI handler for the <c>metta</c> command.
/// Wraps <see cref="MeTTaCommands.RunMeTTaAsync"/> with proper error handling and exit-code semantics.
/// </summary>
public sealed class MeTTaCommandHandler
{
    private readonly ILogger<MeTTaCommandHandler> _logger;

    public MeTTaCommandHandler(ILogger<MeTTaCommandHandler> logger)
    {
        _logger = logger;
    }

    public async Task<int> HandleAsync(MeTTaOptions options, CancellationToken cancellationToken = default)
    {
        try
        {
            await MeTTaCommands.RunMeTTaAsync(options);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "metta command failed");
            return 1;
        }
    }
}
