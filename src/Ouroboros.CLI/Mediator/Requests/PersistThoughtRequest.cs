using MediatR;
using Ouroboros.Application.Personality;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR request to persist a thought to storage.
/// Replaces direct calls to <c>OuroborosAgent.PersistThoughtAsync</c>.
/// </summary>
public sealed record PersistThoughtRequest(InnerThought Thought, string? Topic = null) : IRequest<Unit>;
