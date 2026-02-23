using MediatR;
using Ouroboros.Application.Services;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR request to handle a user presence detection event.
/// Replaces direct calls to <c>OuroborosAgent.HandlePresenceDetectedAsync</c>.
/// </summary>
public sealed record HandlePresenceRequest(PresenceEvent Event) : IRequest<Unit>;
