using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR request to recall conversation memories about a topic.
/// Replaces direct calls to <c>OuroborosAgent.RecallAsync</c>.
/// </summary>
public sealed record RecallRequest(string Topic) : IRequest<string>;
