using MediatR;
using Ouroboros.Abstractions.Monads;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR request to execute a MeTTa symbolic reasoning query.
/// Replaces direct calls to <c>OuroborosAgent.QueryMeTTaResultAsync</c>.
/// </summary>
public sealed record QueryMeTTaRequest(string Query) : IRequest<Result<string, string>>;
