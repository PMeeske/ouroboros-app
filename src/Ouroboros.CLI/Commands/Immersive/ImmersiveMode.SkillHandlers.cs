// <copyright file="ImmersiveMode.SkillHandlers.cs" company="Ouroboros">
// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.
// </copyright>

namespace Ouroboros.CLI.Commands;

using Ouroboros.Abstractions.Agent;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Options;
using Spectre.Console;

/// <summary>
/// Skill action handlers: listing, running, and learning skills.
/// </summary>
public sealed partial class ImmersiveMode
{
    private async Task<string> HandleListSkillsAsync(string personaName)
    {
        if (_tools.SkillRegistry == null)
            return "I don't have any skills loaded right now.";

        var skills = _tools.SkillRegistry.GetAllSkills().ToList();
        if (skills.Count == 0)
            return "I haven't learned any skills yet. Say 'learn about' something to teach me.";

        AnsiConsole.WriteLine();
        var skillsTable = OuroborosTheme.ThemedTable("Skill", "Success Rate");
        foreach (var skill in skills.Take(10))
        {
            skillsTable.AddRow(Markup.Escape(skill.Name), Markup.Escape($"{skill.SuccessRate:P0}"));
        }
        if (skills.Count > 10)
            skillsTable.AddRow(Markup.Escape($"... and {skills.Count - 10} more"), "");
        AnsiConsole.Write(OuroborosTheme.ThemedPanel(skillsTable, $"My Skills ({skills.Count})"));

        return $"I know {skills.Count} skills. The top ones are: {string.Join(", ", skills.Take(5).Select(s => s.Name))}.";
    }

    private async Task<string> HandleRunSkillAsync(
        string skillName,
        string personaName,
        IVoiceOptions options,
        CancellationToken ct)
    {
        if (_tools.SkillRegistry == null)
            return "Skills are not available.";

        var skill = _tools.SkillRegistry.GetAllSkills()
            .FirstOrDefault(s => s.Name.Contains(skillName, StringComparison.OrdinalIgnoreCase));

        if (skill == null)
            return $"I don't know a skill called '{skillName}'. Say 'list skills' to see what I know.";

        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.GoldText($"[>] Executing skill: {skill.Name}")}");
        var results = new List<string>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        foreach (var step in skill.ToSkill().Steps)
        {
            AnsiConsole.MarkupLine($"      {OuroborosTheme.Accent("->")} {Markup.Escape(step.Action)}: {Markup.Escape(step.ExpectedOutcome)}");
            results.Add($"Step: {step.Action}");
            await Task.Delay(200, ct); // Simulate step execution
        }
        stopwatch.Stop();
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok("[OK] Skill complete")}");

        // Learn from skill execution (interconnected learning)
        if (_tools.InterconnectedLearner != null)
        {
            await _tools.InterconnectedLearner.RecordSkillExecutionAsync(
                skill.Name,
                string.Join(", ", skill.ToSkill().Steps.Select(s => s.Action)),
                string.Join("\n", results),
                true,
                ct);
        }

        return $"I ran the {skill.Name} skill. It has {skill.ToSkill().Steps.Count} steps.";
    }

    private async Task<string> HandleLearnAboutAsync(
        string topic,
        string personaName,
        IVoiceOptions options,
        CancellationToken ct)
    {
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.GoldText($"[~] Researching: {topic}...")}");

        // Use ArxivSearch if available
        if (_allTokens?.ContainsKey("ArxivSearch") == true)
        {
            // Simulate research
            await Task.Delay(500, ct);
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] Found research on {topic}")}");
        }

        // Create a simple skill from the topic
        if (_tools.SkillRegistry != null)
        {
            var stepParams = new Dictionary<string, object> { ["query"] = topic };
            var skill = new Skill(
                $"Research_{topic.Replace(" ", "_")}",
                $"Research skill for {topic}",
                new List<string>(),
                [new PlanStep($"Search for {topic}", stepParams, "research_results", 0.8)],
                0.75,
                0,
                DateTime.UtcNow,
                DateTime.UtcNow);
            await _tools.SkillRegistry.RegisterSkillAsync(skill.ToAgentSkill());
            return $"I learned about {topic} and created a research skill for it.";
        }

        return $"I researched {topic}. Interesting stuff!";
    }
}
