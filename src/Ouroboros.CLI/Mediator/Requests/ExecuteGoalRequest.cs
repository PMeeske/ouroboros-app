using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR request to execute a goal via the orchestrator.
/// Replaces direct calls to <c>OuroborosAgent.ExecuteAsync</c>.
/// </summary>
public sealed record ExecuteGoalRequest(string Goal) : IRequest<string>;
