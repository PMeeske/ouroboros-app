using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="PlanRequest"/>.
/// Creates a step-by-step plan via the orchestrator, with LLM fallback.
/// </summary>
public sealed class PlanHandler : IRequestHandler<PlanRequest, string>
{
    private readonly OuroborosAgent _agent;

    public PlanHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<string> Handle(PlanRequest request, CancellationToken ct)
    {
        var orchestrator = _agent.AutonomySub.Orchestrator;
        if (orchestrator == null)
        {
            // Fallback to LLM-based planning
            var llm = _agent.ModelsSub.Llm;
            if (llm != null)
            {
                var (plan, _) = await llm.GenerateWithToolsAsync(
                    $"Create a step-by-step plan for: {request.Goal}. Format as numbered steps.");
                return plan;
            }
            return "I need an orchestrator or LLM to create plans.";
        }

        var planResult = await orchestrator.PlanAsync(request.Goal);
        return planResult.Match(
            plan =>
            {
                var steps = string.Join("\n", plan.Steps.Select((s, i) => $"  {i + 1}. {s.Action}"));
                return $"Here's my plan for '{request.Goal}':\n{steps}";
            },
            error => $"I couldn't plan that: {error}");
    }
}
