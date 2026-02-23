using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR request to generate a personalized greeting at session start.
/// Replaces direct calls to <c>OuroborosAgent.GetGreetingAsync</c>.
/// </summary>
public sealed record GetGreetingRequest() : IRequest<string>;
