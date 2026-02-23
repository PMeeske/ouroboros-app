using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR request to store a conversation memory.
/// Replaces direct calls to <c>OuroborosAgent.RememberAsync</c>.
/// </summary>
public sealed record RememberRequest(string Info) : IRequest<string>;
