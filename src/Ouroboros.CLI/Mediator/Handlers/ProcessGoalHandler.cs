using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="ProcessGoalRequest"/>.
/// Extracted from <c>OuroborosAgent.ProcessGoalAsync</c>.
/// Plans and executes a goal, speaks the response, and records conversation history.
/// </summary>
public sealed class ProcessGoalHandler : IRequestHandler<ProcessGoalRequest>
{
    private readonly OuroborosAgent _agent;
    private readonly IMediator _mediator;

    public ProcessGoalHandler(OuroborosAgent agent, IMediator mediator)
    {
        _agent = agent;
        _mediator = mediator;
    }

    public async Task Handle(ProcessGoalRequest request, CancellationToken cancellationToken)
    {
        var goal = request.Goal;

        // ExecuteAsync remains on the agent — call it indirectly through the agent's mediator
        // ExecuteAsync is a private method that stays on the agent, so we replicate the
        // call chain: the agent's Commands.cs still has ExecuteAsync, and ProcessGoalAsync
        // now dispatches through mediator. The orchestrator logic uses _agent subsystem accessors.
        var orchestrator = _agent.AutonomySub.Orchestrator;
        string response;

        if (orchestrator == null)
        {
            response = await _mediator.Send(new ChatRequest($"Help me accomplish: {goal}"), cancellationToken);
        }
        else
        {
            var planResult = await orchestrator.PlanAsync(goal);
            response = await planResult.Match(
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

        // SayWithVoiceAsync → _agent.VoiceService.SayAsync
        await _agent.VoiceService.SayAsync(response);

        // Say → side channel
        _agent.VoiceSub.SideChannel?.Say(response, _agent.Config.Persona);

        // Update conversation history
        _agent.MemorySub.ConversationHistory.Add($"Goal: {goal}");
        _agent.MemorySub.ConversationHistory.Add($"Ouroboros: {response}");
    }
}
