using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR command that directs the agent to plan and execute a goal.
/// </summary>
public sealed record ProcessGoalRequest(string Goal) : IRequest;
