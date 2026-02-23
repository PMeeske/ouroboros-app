using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR request to create a step-by-step plan for a goal.
/// Replaces direct calls to <c>OuroborosAgent.PlanAsync</c>.
/// </summary>
public sealed record PlanRequest(string Goal) : IRequest<string>;
