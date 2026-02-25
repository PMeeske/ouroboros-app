using MediatR;
using Microsoft.Extensions.Logging;
using Ouroboros.CLI.Services;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="RunMeTTaRequest"/>.
/// Delegates to <see cref="IMeTTaService"/>.
/// </summary>
public sealed class RunMeTTaRequestHandler : IRequestHandler<RunMeTTaRequest>
{
    private readonly IMeTTaService _mettaService;
    private readonly ILogger<RunMeTTaRequestHandler> _logger;

    public RunMeTTaRequestHandler(IMeTTaService mettaService, ILogger<RunMeTTaRequestHandler> logger)
    {
        _mettaService = mettaService;
        _logger = logger;
    }

    public async Task Handle(RunMeTTaRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _mettaService.RunAsync(request.Config, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MeTTa command failed for goal: {Goal}", request.Config.Goal);
            throw;
        }
    }
}
