// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using System.Text;
using Spectre.Console;
using PipelineAgentCapability = Ouroboros.Pipeline.MultiAgent.AgentCapability;
using PipelineExperience = Ouroboros.Pipeline.Learning.Experience;
using PipelineGoal = Ouroboros.Pipeline.Planning.Goal;
using PipelineTaskStatus = Ouroboros.Pipeline.MultiAgent.TaskStatus;

/// <summary>
/// Reasoning partial: learning interaction recording, cognitive event tracking,
/// self-assessment, council debate, and agent coordination.
/// </summary>
public sealed partial class CognitiveSubsystem
{
    // ═══════════════════════════════════════════════════════════════════════════
    // AGI SUBSYSTEM METHODS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Records an interaction for continuous learning.
    /// Called after every chat response to enable the learning agent to track performance.
    /// </summary>
    internal void RecordInteractionForLearning(string input, string response)
    {
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(response))
            return;

        try
        {
            // Estimate quality based on response length and content indicators
            double quality = EstimateResponseQuality(input, response);

            // 1. Record to Learning Agent
            if (LearningAgent != null)
            {
                var result = LearningAgent.RecordInteraction(input, response, quality);
                if (result.IsSuccess && LearningAgent.ShouldAdapt())
                {
                    var adaptResult = LearningAgent.Adapt();
                    if (adaptResult.IsSuccess)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AGI:Learning] Adaptation performed: {adaptResult.Value.EventType}");
                    }
                }
            }

            // 2. Record to Experience Buffer for replay learning
            if (ExperienceBuffer != null)
            {
                var experience = PipelineExperience.Create(
                    state: input,
                    action: response,
                    reward: quality,
                    nextState: "", // Will be populated with next interaction
                    priority: Math.Abs(quality) + 0.1); // Higher priority for extreme outcomes
                ExperienceBuffer.Add(experience);
            }

            // 3. Update Introspection state
            if (Introspector != null)
            {
                // Track cognitive load based on input complexity
                double estimatedLoad = Math.Min(input.Length / 500.0, 1.0);
                Introspector.SetCognitiveLoad(estimatedLoad);

                // Update valence based on interaction quality
                Introspector.SetValence(quality * 0.5);

                // Add to working memory (recent topics)
                var topic = ExtractTopicFromInput(input);
                if (!string.IsNullOrEmpty(topic))
                {
                    Introspector.SetCurrentFocus(topic);
                }
            }

            // 4. Update World State with observation
            if (WorldState != null)
            {
                WorldState = WorldState.WithObservation(
                    $"interaction_{DateTime.UtcNow.Ticks}",
                    Ouroboros.Pipeline.WorldModel.Observation.Create($"User: {TruncateText(input, 50)}", 1.0));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AGI:Learning] Error recording interaction: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts a topic from user input for focus tracking.
    /// </summary>
    internal static string ExtractTopicFromInput(string input)
    {
        // Simple topic extraction - get first few meaningful words
        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .Take(3);
        return string.Join(" ", words);
    }

    /// <summary>
    /// Records a cognitive event for monitoring.
    /// Called after every chat response to enable real-time cognitive health tracking.
    /// </summary>
    internal void RecordCognitiveEvent(string input, string response, List<ToolExecution>? tools)
    {
        if (CognitiveMonitor == null)
            return;

        try
        {
            // Create appropriate cognitive event based on interaction
            var eventType = DetermineCognitiveEventType(input, response, tools);
            var cognitiveEvent = CreateCognitiveEvent(eventType, input, response, tools);

            var result = CognitiveMonitor.RecordEvent(cognitiveEvent);
            if (result.IsFailure)
            {
                System.Diagnostics.Debug.WriteLine($"[AGI:Cognitive] Failed to record event: {result.Error}");
            }

            // Update self-assessor with the interaction
            if (SelfAssessor != null)
            {
                UpdateSelfAssessment(input, response, tools);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AGI:Cognitive] Error recording cognitive event: {ex.Message}");
        }
    }

    /// <summary>
    /// Estimates the quality of a response for learning purposes.
    /// Returns a value between -1.0 (poor) and 1.0 (excellent).
    /// </summary>
    internal static double EstimateResponseQuality(string input, string response)
    {
        double quality = 0.5; // Baseline

        // Length appropriateness (not too short, not excessive)
        int responseLen = response.Length;
        int inputLen = input.Length;
        double lengthRatio = (double)responseLen / Math.Max(inputLen, 1);

        if (lengthRatio >= 1 && lengthRatio <= 10)
            quality += 0.1; // Good length ratio
        else if (lengthRatio < 0.5 || lengthRatio > 50)
            quality -= 0.2; // Too short or excessively long

        // Content indicators
        if (response.Contains("I don't know") || response.Contains("I'm not sure"))
            quality -= 0.1; // Uncertainty penalty (small - it's okay to be honest)

        if (response.Contains("```") || response.Contains("[TOOL:"))
            quality += 0.15; // Code/tool usage indicates substantive response

        if (response.Contains("Error") || response.Contains("failed") || response.Contains("❌"))
            quality -= 0.15; // Error indicators

        if (response.Contains("✓") || response.Contains("✅") || response.Contains("successfully"))
            quality += 0.1; // Success indicators

        // Question handling
        if (input.Contains("?") && response.Length > 50)
            quality += 0.1; // Answered a question with substance

        return Math.Clamp(quality, -1.0, 1.0);
    }

    /// <summary>
    /// Determines the appropriate cognitive event type based on interaction.
    /// </summary>
    internal static CognitiveEventType DetermineCognitiveEventType(
        string input, string response, List<ToolExecution>? tools)
    {
        if (tools?.Any() == true)
            return CognitiveEventType.DecisionMade; // Tool use = decision

        if (response.Contains("Error") || response.Contains("❌") || response.Contains("failed"))
            return CognitiveEventType.ErrorDetected;

        if (response.Contains("I'm not sure") || response.Contains("uncertain") || response.Contains("might"))
            return CognitiveEventType.Uncertainty;

        if (response.Contains("I understand") || response.Contains("insight") || response.Contains("realized"))
            return CognitiveEventType.InsightGained;

        if (input.Contains("?"))
            return CognitiveEventType.GoalActivated; // Question = goal to answer

        return CognitiveEventType.ThoughtGenerated;
    }

    /// <summary>
    /// Creates a cognitive event from interaction data.
    /// </summary>
    internal static CognitiveEvent CreateCognitiveEvent(
        CognitiveEventType eventType, string input, string response, List<ToolExecution>? tools)
    {
        var context = ImmutableDictionary<string, object>.Empty
            .Add("input_length", input.Length)
            .Add("response_length", response.Length)
            .Add("tools_used", tools?.Count ?? 0);

        var description = eventType switch
        {
            CognitiveEventType.DecisionMade => $"Made decision using {tools?.Count ?? 0} tool(s)",
            CognitiveEventType.ErrorDetected => "Error detected in processing",
            CognitiveEventType.Uncertainty => "Uncertainty expressed in response",
            CognitiveEventType.InsightGained => "New insight or understanding achieved",
            CognitiveEventType.GoalActivated => $"Processing query: {TruncateText(input, 50)}",
            _ => $"Generated response: {TruncateText(response, 50)}"
        };

        return new CognitiveEvent(
            Id: Guid.NewGuid(),
            EventType: eventType,
            Description: description,
            Timestamp: DateTime.UtcNow,
            Severity: eventType == CognitiveEventType.ErrorDetected ? Severity.Warning : Severity.Info,
            Context: context);
    }

    /// <summary>
    /// Updates the Bayesian self-assessor with interaction data.
    /// </summary>
    internal void UpdateSelfAssessment(string input, string response, List<ToolExecution>? tools)
    {
        if (SelfAssessor == null)
            return;

        // Update each capability based on interaction characteristics
        double responseQuality = EstimateResponseQuality(input, response);

        // Accuracy - based on tool success rate
        if (tools?.Any() == true)
        {
            double toolSuccessRate = tools.Count(t => !t.Output.Contains("Error") && !t.Output.Contains("failed")) / (double)tools.Count;
            SelfAssessor.UpdateBelief("tool_accuracy", toolSuccessRate);
        }

        // Response quality as a capability
        SelfAssessor.UpdateBelief("response_quality", Math.Max(0, (responseQuality + 1.0) / 2.0)); // Normalize to [0,1]

        // Coherence - basic heuristic based on response structure
        double coherence = response.Contains("\n") || response.Length > 100 ? 0.7 : 0.5;
        SelfAssessor.UpdateBelief("coherence", coherence);
    }

    /// <summary>
    /// Helper to truncate text for display.
    /// </summary>
    internal static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return text[..maxLength] + "...";
    }

    /// <summary>
    /// Runs a multi-agent council debate on a topic.
    /// Uses the Round Table protocol with 5 debate phases.
    /// </summary>
    internal async Task<string> RunCouncilDebateAsync(string topic)
    {
        if (CouncilOrchestrator == null)
        {
            return "❌ Council Orchestrator not available. LLM may not be initialized.";
        }

        if (string.IsNullOrWhiteSpace(topic))
        {
            return @"🏛️ **Multi-Agent Council Debate**

Usage: `council <topic>` or `debate <topic>`

The Council uses the Round Table Protocol with 5 phases:
  1. Opening statements from each agent
  2. Cross-examination and challenges
  3. Rebuttals and counter-arguments
  4. Synthesis of viewpoints
  5. Final consensus or dissent

Examples:
  `council Should we prioritize code quality over speed?`
  `debate What is the best approach to handle errors in this system?`
  `council How should we balance user experience with security?`";
        }

        try
        {
            AnsiConsole.MarkupLine($"\n[rgb(148,103,189)]🏛️ Initiating Council Debate on: {Markup.Escape(topic)}[/]\n");

            // Create topic and start the debate
            var councilTopic = CouncilTopic.Simple(topic);
            var result = await CouncilOrchestrator.ConveneCouncilAsync(councilTopic);

            if (result.IsFailure)
            {
                return $"❌ Council debate failed: {result.Error}";
            }

            var decision = result.Value;
            var sb = new StringBuilder();
            sb.AppendLine($"🏛️ **Council Deliberation: {TruncateText(topic, 50)}**\n");

            // Show debate transcript summary
            sb.AppendLine("═══ Debate Transcript ═══");
            foreach (var round in decision.Transcript.Take(3))
            {
                sb.AppendLine($"\n**{round.Phase}**:");
                foreach (var contrib in round.Contributions.Take(3))
                {
                    sb.AppendLine($"  • {contrib.AgentName}: {TruncateText(contrib.Content, 150)}");
                }
            }

            // Show votes
            sb.AppendLine("\n═══ Agent Votes ═══");
            foreach (var vote in decision.Votes.Values)
            {
                sb.AppendLine($"  • {vote.AgentName}: {vote.Position} (weight: {vote.Weight:F2})");
                sb.AppendLine($"    Rationale: {TruncateText(vote.Rationale, 100)}");
            }

            // Show final decision
            sb.AppendLine("\n═══ Council Decision ═══");
            sb.AppendLine($"**Conclusion**: {decision.Conclusion}");
            sb.AppendLine($"**Confidence**: {decision.Confidence:P0}");
            sb.AppendLine($"**Consensus**: {(decision.IsConsensus ? "Yes ✓" : "No")}");

            if (decision.MinorityOpinions.Count > 0)
            {
                sb.AppendLine($"\n**Minority Opinions** ({decision.MinorityOpinions.Count}):");
                foreach (var minority in decision.MinorityOpinions.Take(2))
                {
                    sb.AppendLine($"  • {minority.AgentName}: {TruncateText(minority.Rationale, 100)}");
                }
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Council debate failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Runs multi-agent coordination on a goal.
    /// </summary>
    internal async Task<string> RunAgentCoordinationAsync(string goalDescription)
    {
        if (AgentCoordinator == null)
        {
            return "❌ Agent Coordinator not initialized.";
        }

        if (string.IsNullOrWhiteSpace(goalDescription))
        {
            return @"🤝 **Multi-Agent Coordination**

Usage: `coordinate <goal>`

The Agent Coordinator decomposes complex goals and distributes tasks
across a team of specialized agents.

Team Members:
  • Primary - Main reasoning and analysis
  • Critic - Critical evaluation of solutions
  • Researcher - Information gathering

Examples:
  `coordinate Analyze the performance of this codebase`
  `coordinate Create a comprehensive test plan`
  `coordinate Identify potential security vulnerabilities`";
        }

        try
        {
            AnsiConsole.MarkupLine($"\n[rgb(148,103,189)]🤝 Coordinating agents for: {Markup.Escape(TruncateText(goalDescription, 50))}[/]\n");

            var goal = PipelineGoal.Atomic(goalDescription);
            var result = await AgentCoordinator.ExecuteAsync(goal);

            if (result.IsFailure)
            {
                return $"❌ Coordination failed: {result.Error}";
            }

            var coordination = result.Value;
            var sb = new StringBuilder();
            sb.AppendLine($"🤝 **Coordination Result**\n");
            sb.AppendLine($"═══ Summary ═══");
            sb.AppendLine($"  • Goal: {TruncateText(goalDescription, 60)}");
            sb.AppendLine($"  • Status: {(coordination.IsSuccess ? "✓ Success" : "✗ Failed")}");
            sb.AppendLine($"  • Duration: {coordination.TotalDuration.TotalSeconds:F2}s");
            sb.AppendLine($"  • Tasks: {coordination.CompletedTaskCount}/{coordination.Tasks.Count} completed");
            sb.AppendLine($"  • Agents: {coordination.ParticipatingAgents.Count} participated");

            if (coordination.Tasks.Count > 0)
            {
                sb.AppendLine($"\n═══ Tasks ═══");
                foreach (var task in coordination.Tasks.Take(5))
                {
                    var statusIcon = task.Status switch
                    {
                        PipelineTaskStatus.Completed => "✓",
                        PipelineTaskStatus.Failed => "✗",
                        PipelineTaskStatus.InProgress => "⟳",
                        _ => "○"
                    };
                    sb.AppendLine($"  {statusIcon} {task.Goal.Description}");
                    if (task.Result.HasValue)
                    {
                        sb.AppendLine($"    Result: {TruncateText(task.Result.Value ?? "", 80)}");
                    }
                }
            }

            sb.AppendLine($"\n═══ Final Summary ═══");
            sb.AppendLine($"  {coordination.Summary}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Coordination failed: {ex.Message}";
        }
    }
}
