using MediatR;
using Microsoft.Extensions.Logging;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="RunMeTTaRequest"/>.
/// Wraps <see cref="Ouroboros.CLI.Commands.MeTTaCommands.RunMeTTaAsync"/>
/// with proper error handling.
/// </summary>
public sealed class RunMeTTaRequestHandler : IRequestHandler<RunMeTTaRequest>
{
    private readonly ILogger<RunMeTTaRequestHandler> _logger;

    public RunMeTTaRequestHandler(ILogger<RunMeTTaRequestHandler> logger)
    {
        _logger = logger;
    }

    public async Task Handle(RunMeTTaRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await Ouroboros.CLI.Commands.MeTTaCommands.RunMeTTaAsync(request.Options);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MeTTa command failed for goal: {Goal}", request.Options.Goal);
            throw;
        }
    }
}
