using System.Net.Http;
using System.Text;
using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="LearnTopicRequest"/>.
/// Learns about a topic by researching via LLM, creating tools, registering skills,
/// adding MeTTa knowledge, and tracking in the global workspace.
/// </summary>
public sealed class LearnTopicHandler : IRequestHandler<LearnTopicRequest, string>
{
    private readonly OuroborosAgent _agent;

    public LearnTopicHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<string> Handle(LearnTopicRequest request, CancellationToken ct)
    {
        var topic = request.Topic;

        if (string.IsNullOrWhiteSpace(topic))
            return "What would you like me to learn about?";

        var skills = _agent.MemorySub.Skills;
        var llm = _agent.ModelsSub.Llm;
        var toolLearner = _agent.ToolsSub.ToolLearner;
        var tools = _agent.ToolsSub.Tools;
        var mettaEngine = _agent.MemorySub.MeTTaEngine;
        var globalWorkspace = _agent.AutonomySub.GlobalWorkspace;
        var capabilityRegistry = _agent.AutonomySub.CapabilityRegistry;

        var sb = new StringBuilder();
        sb.AppendLine($"Learning about: {topic}");

        // Step 1: Research the topic via LLM
        string? research = null;
        if (llm != null)
        {
            try
            {
                var (response, toolCalls) = await llm.GenerateWithToolsAsync(
                    $"Research and explain key concepts about: {topic}. Include practical applications and how this knowledge could be used.");
                research = response;
                sb.AppendLine($"\n\ud83d\udcda Research Summary:\n{response[..Math.Min(500, response.Length)]}...");
            }
            catch (HttpRequestException ex)
            {
                sb.AppendLine($"\u26a0 Research phase had issues: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                sb.AppendLine($"\u26a0 Research phase had issues: {ex.Message}");
            }
        }

        // Step 2: Try to create a tool capability
        if (toolLearner != null)
        {
            try
            {
                var toolResult = await toolLearner.FindOrCreateToolAsync(topic, tools);
                toolResult.Match(
                    success =>
                    {
                        sb.AppendLine($"\n\ud83d\udd27 {(success.WasCreated ? "Created new" : "Found existing")} tool: '{success.Tool.Name}'");
                        _agent.AddToolAndRefreshLlm(success.Tool);
                    },
                    error => sb.AppendLine($"\u26a0 Tool creation: {error}"));
            }
            catch (InvalidOperationException ex)
            {
                sb.AppendLine($"\u26a0 Tool learner: {ex.Message}");
            }
        }

        // Step 3: Register as a skill if we have skill registry
        if (skills != null && !string.IsNullOrWhiteSpace(research))
        {
            try
            {
                var skillName = SanitizeSkillName(topic);
                var existingSkill = skills.GetSkill(skillName);

                if (existingSkill == null)
                {
                    var skill = new Skill(
                        Name: skillName,
                        Description: $"Knowledge about {topic}: {research[..Math.Min(200, research.Length)]}",
                        Prerequisites: new List<string>(),
                        Steps: new List<PlanStep>
                        {
                            new PlanStep(
                                $"Apply knowledge about {topic}",
                                new Dictionary<string, object> { ["topic"] = topic, ["research"] = research },
                                $"Use {topic} knowledge effectively",
                                0.7)
                        },
                        SuccessRate: 0.8,
                        UsageCount: 0,
                        CreatedAt: DateTime.UtcNow,
                        LastUsed: DateTime.UtcNow);

                    await skills.RegisterSkillAsync(skill.ToAgentSkill());
                    sb.AppendLine($"\n\u2713 Registered skill: '{skillName}'");
                }
                else
                {
                    skills.RecordSkillExecution(skillName, true, 0L);
                    sb.AppendLine($"\n\u21ba Updated existing skill: '{skillName}'");
                }
            }
            catch (InvalidOperationException ex)
            {
                sb.AppendLine($"\u26a0 Skill registration: {ex.Message}");
            }
        }

        // Step 4: Add to MeTTa knowledge base
        if (mettaEngine != null)
        {
            try
            {
                var atomName = SanitizeSkillName(topic);
                await mettaEngine.AddFactAsync($"(: {atomName} Concept)");
                await mettaEngine.AddFactAsync($"(learned {atomName} \"{DateTime.UtcNow:O}\")");

                if (!string.IsNullOrWhiteSpace(research))
                {
                    var summary = research.Length > 100 ? research[..100].Replace("\"", "'") : research.Replace("\"", "'");
                    await mettaEngine.AddFactAsync($"(summary {atomName} \"{summary}\")");
                }

                sb.AppendLine($"\n\ud83e\udde0 Added to MeTTa knowledge base: {atomName}");
            }
            catch (InvalidOperationException ex)
            {
                sb.AppendLine($"\u26a0 MeTTa: {ex.Message}");
            }
        }

        // Step 5: Track in global workspace
        globalWorkspace?.AddItem(
            $"Learned: {topic}\n{research?[..Math.Min(200, research?.Length ?? 0)]}",
            WorkspacePriority.Normal,
            "learning",
            new List<string> { "learned", topic.ToLowerInvariant().Replace(" ", "-") });

        // Step 6: Update capability if available
        if (capabilityRegistry != null)
        {
            var result = AutonomySubsystem.CreateCapabilityPlanExecutionResult(true, TimeSpan.FromSeconds(2), $"learn:{topic}");
            await capabilityRegistry.UpdateCapabilityAsync("natural_language", result);
        }

        return sb.ToString();
    }

    private static string SanitizeSkillName(string name)
    {
        return name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("'", "")
            .Replace("\"", "")
            .Replace("(", "")
            .Replace(")", "");
    }
}
