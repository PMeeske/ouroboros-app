using MediatR;
using Ouroboros.Abstractions.Monads;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR request to execute a MeTTa expression directly.
/// Replaces direct calls to <c>OuroborosAgent.RunMeTTaExpressionResultAsync</c>.
/// </summary>
public sealed record RunMeTTaExpressionResultRequest(string Expression) : IRequest<Result<string, string>>;
