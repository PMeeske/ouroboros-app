using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR request to run the autonomous agent (AutoAgent) pipeline step.
/// Converts the static <c>AgentCliSteps.AutoAgent</c> into a handler-dispatchable request.
/// </summary>
/// <param name="Task">The task description for the agent to execute.</param>
/// <param name="MaxIterations">Maximum number of plan-act iterations (default: 15).</param>
public sealed record RunAutoAgentRequest(
    string Task,
    int MaxIterations = 15) : IRequest<string>;
