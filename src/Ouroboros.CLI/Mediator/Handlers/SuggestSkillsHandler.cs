using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="SuggestSkillsRequest"/>.
/// Finds and suggests skills matching a given goal.
/// </summary>
public sealed class SuggestSkillsHandler : IRequestHandler<SuggestSkillsRequest, string>
{
    private readonly OuroborosAgent _agent;

    public SuggestSkillsHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<string> Handle(SuggestSkillsRequest request, CancellationToken ct)
    {
        var goal = request.Goal;
        var skills = _agent.MemorySub.Skills;

        if (skills == null) return "Skills not available.";

        var matches = await skills.FindMatchingSkillsAsync(goal);
        if (!matches.Any())
            return $"I don't have skills matching '{goal}' yet. Try learning about it first!";

        var suggestions = string.Join(", ", matches.Take(5).Select(s => s.Name));
        return $"For '{goal}', I'd suggest: {suggestions}";
    }
}
