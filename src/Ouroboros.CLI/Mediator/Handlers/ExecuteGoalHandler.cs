using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="ExecuteGoalRequest"/>.
/// Executes a goal via the orchestrator, with ChatRequest fallback.
/// </summary>
public sealed class ExecuteGoalHandler : IRequestHandler<ExecuteGoalRequest, string>
{
    private readonly OuroborosAgent _agent;
    private readonly IMediator _mediator;

    public ExecuteGoalHandler(OuroborosAgent agent, IMediator mediator)
    {
        _agent = agent;
        _mediator = mediator;
    }

    public async Task<string> Handle(ExecuteGoalRequest request, CancellationToken ct)
    {
        var orchestrator = _agent.AutonomySub.Orchestrator;
        if (orchestrator == null)
            return await _mediator.Send(new ChatRequest($"Help me accomplish: {request.Goal}"), ct);

        var planResult = await orchestrator.PlanAsync(request.Goal);
        return await planResult.Match(
            async plan =>
            {
                var execResult = await orchestrator.ExecuteAsync(plan);
                return execResult.Match(
                    result => result.Success
                        ? $"Done! {result.FinalOutput ?? "Goal accomplished."}"
                        : $"Partially completed: {result.FinalOutput}",
                    error => $"Execution failed: {error}");
            },
            error => Task.FromResult($"Couldn't plan: {error}"));
    }
}
