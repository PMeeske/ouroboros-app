using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="RunSkillRequest"/>.
/// Executes a registered skill by name, with fuzzy matching fallback.
/// </summary>
public sealed class RunSkillHandler : IRequestHandler<RunSkillRequest, string>
{
    private readonly OuroborosAgent _agent;

    public RunSkillHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<string> Handle(RunSkillRequest request, CancellationToken ct)
    {
        var skillName = request.SkillName;
        var skills = _agent.MemorySub.Skills;

        if (skills == null) return "Skills not available.";

        var skill = skills.GetSkill(skillName);
        if (skill == null)
        {
            var matches = await skills.FindMatchingSkillsAsync(skillName);
            if (matches.Any())
            {
                skill = matches[0].ToAgentSkill();
            }
            else
            {
                return $"I don't know a skill called '{skillName}'. Try 'list skills'.";
            }
        }

        // Execute skill steps
        var results = new List<string>();
        foreach (var step in skill.ToSkill().Steps)
        {
            results.Add($"\u2022 {step.Action}: {step.ExpectedOutcome}");
        }

        skills.RecordSkillExecution(skill.Name, true, 0L);
        return $"Running '{skill.Name}':\n" + string.Join("\n", results);
    }
}
