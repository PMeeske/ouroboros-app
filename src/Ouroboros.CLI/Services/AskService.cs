using MediatR;
using Microsoft.Extensions.Logging;
using Ouroboros.CLI.Mediator;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Implementation of <see cref="IAskService"/>.
/// Dispatches ask requests through <see cref="IMediator"/> to
/// <see cref="AskQueryHandler"/>, which owns the pipeline and agent-mode logic.
/// </summary>
public class AskService : IAskService
{
    private readonly IMediator _mediator;
    private readonly ILogger<AskService> _logger;

    public AskService(IMediator mediator, ILogger<AskService> logger)
    {
        _mediator = mediator;
        _logger   = logger;
    }

    /// <inheritdoc/>
    public Task<string> AskAsync(AskRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "AskService: model={Model} rag={Rag} agent={Agent}",
            request.ModelName, request.UseRag, request.AgentMode);

        return _mediator.Send(new AskQuery(request), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<string> AskAsync(string question, bool useRag = false)
        => AskAsync(new AskRequest(Question: question, UseRag: useRag));
}
