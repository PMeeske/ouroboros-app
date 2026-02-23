using MediatR;
using Ouroboros.Abstractions.Monads;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR request to ask a question via the IAskService.
/// Replaces direct calls to <c>OuroborosAgent.AskResultAsync</c>.
/// </summary>
public sealed record AskResultRequest(string Question) : IRequest<Result<string, string>>;
