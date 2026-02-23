using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="ListSkillsRequest"/>.
/// Lists all registered skills.
/// </summary>
public sealed class ListSkillsHandler : IRequestHandler<ListSkillsRequest, string>
{
    private readonly OuroborosAgent _agent;

    public ListSkillsHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<string> Handle(ListSkillsRequest request, CancellationToken ct)
    {
        var skills = _agent.MemorySub.Skills;

        if (skills == null) return "I don't have a skill registry set up yet.";

        var allSkills = await skills.FindMatchingSkillsAsync("", null);
        if (!allSkills.Any())
            return "I haven't learned any skills yet. Try 'learn about' something!";

        var list = string.Join(", ", allSkills.Take(10).Select(s => s.Name));
        return $"I know {allSkills.Count} skills: {list}" + (allSkills.Count > 10 ? "..." : "");
    }
}
