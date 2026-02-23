using MediatR;
using Ouroboros.Abstractions.Monads;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="QueryMeTTaRequest"/>.
/// Executes a MeTTa symbolic reasoning query.
/// </summary>
public sealed class QueryMeTTaHandler : IRequestHandler<QueryMeTTaRequest, Result<string, string>>
{
    private readonly OuroborosAgent _agent;

    public QueryMeTTaHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<Result<string, string>> Handle(QueryMeTTaRequest request, CancellationToken ct)
    {
        var mettaEngine = _agent.MemorySub.MeTTaEngine;
        if (mettaEngine == null)
            return Result<string, string>.Failure("MeTTa symbolic reasoning isn't available.");

        return await mettaEngine.ExecuteQueryAsync(request.Query, ct);
    }
}
