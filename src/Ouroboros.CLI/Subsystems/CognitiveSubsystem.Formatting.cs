// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using System.Text;

/// <summary>
/// Formatting partial: AGI status display, introspection reports, world model status,
/// experience buffer status, prompt optimizer status, and display helpers.
/// </summary>
public sealed partial class CognitiveSubsystem
{
    /// <summary>
    /// Gets the AGI subsystems status.
    /// </summary>
    internal string GetAgiStatus()
    {
        var sb = new StringBuilder();
        sb.AppendLine("🧠 **AGI Subsystems Status**\n");

        // Learning Agent
        sb.AppendLine("═══ Continuous Learning ═══");
        if (LearningAgent != null)
        {
            var perf = LearningAgent.GetPerformance();
            sb.AppendLine($"  ✓ Status: Active");
            sb.AppendLine($"  • Total interactions: {perf.TotalInteractions}");
            sb.AppendLine($"  • Success rate: {perf.SuccessRate:P1}");
            sb.AppendLine($"  • Avg quality: {perf.AverageResponseQuality:F3}");
            sb.AppendLine($"  • Performance trend: {perf.CalculateTrend():+0.000;-0.000;0.000}");
            sb.AppendLine($"  • Stagnating: {(perf.IsStagnating() ? "Yes ⚠" : "No")}");
            sb.AppendLine($"  • Adaptations: {LearningAgent.GetAdaptationHistory().Count}");
        }
        else
        {
            sb.AppendLine("  ✗ Not initialized");
        }

        // Meta-Learner
        sb.AppendLine("\n═══ Meta-Learning ═══");
        if (MetaLearner != null)
        {
            sb.AppendLine($"  ✓ Status: Active");
            sb.AppendLine($"  • Strategy: Bayesian-inspired UCB exploration");
            sb.AppendLine($"  • Auto-adapts hyperparameters based on performance");
        }
        else
        {
            sb.AppendLine("  ✗ Not initialized");
        }

        // Cognitive Monitor
        sb.AppendLine("\n═══ Cognitive Monitoring ═══");
        if (CognitiveMonitor != null)
        {
            var health = CognitiveMonitor.GetHealth();
            sb.AppendLine($"  ✓ Status: Active");
            sb.AppendLine($"  • Health: {health.Status} ({health.HealthScore:P0})");
            sb.AppendLine($"  • Error rate: {health.ErrorRate:P1}");
            sb.AppendLine($"  • Efficiency: {health.ProcessingEfficiency:P0}");
            sb.AppendLine($"  • Active alerts: {health.ActiveAlerts.Count}");
            var recentEvents = CognitiveMonitor.GetRecentEvents(5);
            if (recentEvents.Count > 0)
            {
                sb.AppendLine($"  • Recent events: {string.Join(", ", recentEvents.Select(e => e.EventType.ToString()))}");
            }
        }
        else
        {
            sb.AppendLine("  ✗ Not initialized");
        }

        // Self-Assessor
        sb.AppendLine("\n═══ Self-Assessment ═══");
        if (SelfAssessor != null)
        {
            sb.AppendLine($"  ✓ Status: Active");
            var beliefs = SelfAssessor.GetAllBeliefs();
            sb.AppendLine($"  • Tracked capabilities: {beliefs.Count}");
            foreach (var belief in beliefs.Take(4))
            {
                sb.AppendLine($"    - {belief.Key}: {belief.Value.Proficiency:P0} (±{belief.Value.Uncertainty:P0})");
            }
        }
        else
        {
            sb.AppendLine("  ✗ Not initialized");
        }

        // Council Orchestrator
        sb.AppendLine("\n═══ Multi-Agent Council ═══");
        if (CouncilOrchestrator != null)
        {
            sb.AppendLine($"  ✓ Status: Active");
            sb.AppendLine($"  • Agents: {CouncilOrchestrator.Agents.Count}");
            sb.AppendLine($"  • Debate protocol: Round Table (5 phases)");
            sb.AppendLine($"  • Use: `council <topic>` to start a debate");
        }
        else
        {
            sb.AppendLine("  ✗ Not initialized (requires LLM)");
        }

        // Experience Buffer
        sb.AppendLine("\n═══ Experience Replay ═══");
        if (ExperienceBuffer != null)
        {
            sb.AppendLine($"  ✓ Status: Active");
            sb.AppendLine($"  • Buffer size: {ExperienceBuffer.Count}/{ExperienceBuffer.Capacity}");
            sb.AppendLine($"  • Supports: Uniform & prioritized sampling");
        }
        else
        {
            sb.AppendLine("  ✗ Not initialized");
        }

        // Cognitive Introspector
        sb.AppendLine("\n═══ Introspection Engine ═══");
        if (Introspector != null)
        {
            sb.AppendLine($"  ✓ Status: Active");
            var stateResult = Introspector.CaptureState();
            if (stateResult.IsSuccess)
            {
                var state = stateResult.Value;
                sb.AppendLine($"  • Processing mode: {state.Mode}");
                sb.AppendLine($"  • Cognitive load: {state.CognitiveLoad:P0}");
                sb.AppendLine($"  • Active goals: {state.ActiveGoals.Count}");
                sb.AppendLine($"  • Working memory: {state.WorkingMemoryItems.Count} items");
            }
            sb.AppendLine($"  • Use: `introspect` for deep self-analysis");
        }
        else
        {
            sb.AppendLine("  ✗ Not initialized");
        }

        // World State
        sb.AppendLine("\n═══ World Model ═══");
        if (WorldState != null)
        {
            sb.AppendLine($"  ✓ Status: Active");
            sb.AppendLine($"  • Observations: {WorldState.Observations.Count}");
            sb.AppendLine($"  • Capabilities: {WorldState.Capabilities.Count}");
            sb.AppendLine($"  • Environment tracking enabled");
        }
        else
        {
            sb.AppendLine("  ✗ Not initialized");
        }

        // Smart Tool Selector
        sb.AppendLine("\n═══ Smart Tool Selection ═══");
        if (ToolsSub.SmartToolSelector != null)
        {
            sb.AppendLine($"  ✓ Status: Active");
            sb.AppendLine($"  • Strategy: {ToolsSub.SmartToolSelector.Configuration.OptimizeFor}");
            sb.AppendLine($"  • Max tools: {ToolsSub.SmartToolSelector.Configuration.MaxTools}");
            sb.AppendLine($"  • Min confidence: {ToolsSub.SmartToolSelector.Configuration.MinConfidence:P0}");
        }
        else
        {
            sb.AppendLine("  ✗ Not initialized");
        }

        // Agent Coordinator
        sb.AppendLine("\n═══ Agent Coordination ═══");
        if (AgentCoordinator != null)
        {
            sb.AppendLine($"  ✓ Status: Active");
            sb.AppendLine($"  • Team size: {AgentCoordinator.Team.Count} agents");
            foreach (var id in AgentCoordinator.Team.GetAllAgents().Take(3).Select(a => a.Identity))
            {
                sb.AppendLine($"    - {id.Name} ({id.Role})");
            }
            sb.AppendLine($"  • Use: `coordinate <goal>` for multi-agent tasks");
        }
        else
        {
            sb.AppendLine("  ✗ Not initialized");
        }

        // Commands summary
        sb.AppendLine("\n═══ AGI Commands ═══");
        sb.AppendLine("  • `agi status` - This status report");
        sb.AppendLine("  • `council <topic>` - Multi-agent debate");
        sb.AppendLine("  • `introspect` - Deep self-analysis");
        sb.AppendLine("  • `world` - World model state");
        sb.AppendLine("  • `coordinate <goal>` - Multi-agent coordination");

        return sb.ToString();
    }

    /// <summary>
    /// Gets a detailed introspection report showing current cognitive state and analysis.
    /// </summary>
    internal string GetIntrospectionReport()
    {
        if (Introspector == null)
        {
            return "❌ Introspection Engine not initialized.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("🔍 **Deep Introspection Report**\n");

        // Capture current state
        var stateResult = Introspector.CaptureState();
        if (stateResult.IsFailure)
        {
            return $"❌ Failed to capture cognitive state: {stateResult.Error}";
        }

        var state = stateResult.Value;
        sb.AppendLine("═══ Current Cognitive State ═══");
        sb.AppendLine($"  • Processing Mode: {state.Mode}");
        sb.AppendLine($"  • Cognitive Load: {state.CognitiveLoad:P0}");
        sb.AppendLine($"  • Emotional Valence: {state.EmotionalValence:+0.00;-0.00;0.00}");
        sb.AppendLine($"  • Current Focus: {state.CurrentFocus}");

        if (state.ActiveGoals.Count > 0)
        {
            sb.AppendLine($"\n═══ Active Goals ({state.ActiveGoals.Count}) ═══");
            foreach (var goal in state.ActiveGoals.Take(5))
            {
                sb.AppendLine($"  • {goal}");
            }
        }

        if (state.WorkingMemoryItems.Count > 0)
        {
            sb.AppendLine($"\n═══ Working Memory ({state.WorkingMemoryItems.Count} items) ═══");
            foreach (var item in state.WorkingMemoryItems.Take(5))
            {
                sb.AppendLine($"  • {TruncateText(item, 60)}");
            }
        }

        if (state.AttentionDistribution.Count > 0)
        {
            sb.AppendLine($"\n═══ Attention Distribution ═══");
            foreach (var (area, weight) in state.AttentionDistribution.OrderByDescending(x => x.Value).Take(5))
            {
                sb.AppendLine($"  • {area}: {weight:P0}");
            }
        }

        // Analyze the state
        var analysisResult = Introspector.Analyze(state);
        if (analysisResult.IsSuccess)
        {
            var report = analysisResult.Value;
            if (report.Observations.Count > 0)
            {
                sb.AppendLine($"\n═══ Observations ═══");
                foreach (var obs in report.Observations.Take(5))
                {
                    sb.AppendLine($"  • {obs}");
                }
            }

            if (report.Anomalies.Count > 0)
            {
                sb.AppendLine($"\n═══ ⚠ Anomalies Detected ═══");
                foreach (var anomaly in report.Anomalies)
                {
                    sb.AppendLine($"  ⚠ {anomaly}");
                }
            }

            if (report.Recommendations.Count > 0)
            {
                sb.AppendLine($"\n═══ Recommendations ═══");
                foreach (var rec in report.Recommendations.Take(3))
                {
                    sb.AppendLine($"  → {rec}");
                }
            }

            sb.AppendLine($"\n═══ Self-Assessment Score: {report.SelfAssessmentScore:P0} ═══");
        }

        // Get state history patterns
        var historyResult = Introspector.GetStateHistory();
        if (historyResult.IsSuccess && historyResult.Value.Count > 1)
        {
            sb.AppendLine($"\n═══ State History ({historyResult.Value.Count} snapshots) ═══");
            var patternResult = Introspector.IdentifyPatterns(historyResult.Value);
            if (patternResult.IsSuccess && patternResult.Value.Count > 0)
            {
                sb.AppendLine("Detected Patterns:");
                foreach (var pattern in patternResult.Value.Take(3))
                {
                    sb.AppendLine($"  • {pattern}");
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets the current world model state.
    /// </summary>
    internal string GetWorldModelStatus()
    {
        if (WorldState == null)
        {
            return "❌ World State not initialized.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("🌍 **World Model State**\n");

        sb.AppendLine("═══ Environment Observations ═══");
        if (WorldState.Observations.Count == 0)
        {
            sb.AppendLine("  No observations recorded yet.");
        }
        else
        {
            foreach (var (key, obs) in WorldState.Observations.Take(10))
            {
                sb.AppendLine($"  • {key}: {obs.Value} (confidence: {obs.Confidence:P0}, {FormatTimeAgo(obs.Timestamp)})");
            }
        }

        sb.AppendLine($"\n═══ Known Capabilities ({WorldState.Capabilities.Count}) ═══");
        if (WorldState.Capabilities.Count == 0)
        {
            sb.AppendLine("  No capabilities registered.");
        }
        else
        {
            foreach (var cap in WorldState.Capabilities.Take(10))
            {
                sb.AppendLine($"  • {cap.Name}: {cap.Description}");
                if (cap.RequiredTools.Count > 0)
                {
                    sb.AppendLine($"    Tools: {string.Join(", ", cap.RequiredTools)}");
                }
            }
        }

        // Smart tool selector info
        if (ToolsSub.SmartToolSelector != null)
        {
            sb.AppendLine($"\n═══ Smart Tool Selection ═══");
            sb.AppendLine($"  • Optimization: {ToolsSub.SmartToolSelector.Configuration.OptimizeFor}");
            sb.AppendLine($"  • Max tools per goal: {ToolsSub.SmartToolSelector.Configuration.MaxTools}");
            sb.AppendLine($"  • Min confidence: {ToolsSub.SmartToolSelector.Configuration.MinConfidence:P0}");
            sb.AppendLine($"  • Parallel execution: {(ToolsSub.SmartToolSelector.Configuration.AllowParallelExecution ? "Yes" : "No")}");
        }

        // Tool capability matcher
        if (ToolsSub.ToolCapabilityMatcher != null && ToolsSub.Tools != null)
        {
            sb.AppendLine($"\n═══ Tool Capability Index ═══");
            sb.AppendLine($"  • Indexed tools: {ToolsSub.Tools.Count}");
            sb.AppendLine($"  • Ready for goal-based tool selection");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets the experience buffer status and recent experiences.
    /// </summary>
    internal string GetExperienceBufferStatus()
    {
        if (ExperienceBuffer == null)
        {
            return "❌ Experience Buffer not initialized.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("💾 **Experience Replay Buffer**\n");

        sb.AppendLine("═══ Buffer Status ═══");
        sb.AppendLine($"  • Size: {ExperienceBuffer.Count}/{ExperienceBuffer.Capacity}");
        sb.AppendLine($"  • Fill rate: {(double)ExperienceBuffer.Count / ExperienceBuffer.Capacity:P0}");
        sb.AppendLine($"  • Sampling modes: Uniform, Prioritized (α=0.6)");

        // Sample some recent experiences
        if (ExperienceBuffer.Count > 0)
        {
            var samples = ExperienceBuffer.Sample(Math.Min(5, ExperienceBuffer.Count));
            sb.AppendLine($"\n═══ Recent Experiences (sample of {samples.Count}) ═══");
            foreach (var exp in samples)
            {
                var rewardIcon = exp.Reward > 0.5 ? "✓" : exp.Reward < -0.2 ? "✗" : "○";
                sb.AppendLine($"  {rewardIcon} [{exp.Timestamp:HH:mm:ss}] Reward: {exp.Reward:+0.00;-0.00;0.00}");
                sb.AppendLine($"    State: {TruncateText(exp.State, 40)}");
                sb.AppendLine($"    Action: {TruncateText(exp.Action, 40)}");
            }
        }

        sb.AppendLine($"\n═══ Usage ═══");
        sb.AppendLine("  Experiences are automatically recorded during interactions.");
        sb.AppendLine("  Used for replay-based learning and performance optimization.");

        return sb.ToString();
    }

    /// <summary>
    /// Gets the prompt optimizer status and learned patterns.
    /// </summary>
    internal string GetPromptOptimizerStatus()
    {
        var sb = new StringBuilder();
        sb.AppendLine("🧠 **Runtime Prompt Optimization System**\n");
        sb.AppendLine(ToolsSub.PromptOptimizer.GetStatistics());

        sb.AppendLine("\n═══ How It Works ═══");
        sb.AppendLine("  • Tracks whether tools are called when expected");
        sb.AppendLine("  • Uses Thompson Sampling (multi-armed bandit) to select best patterns");
        sb.AppendLine("  • Adapts instruction emphasis based on success/failure rates");
        sb.AppendLine("  • Learns from recent failures to avoid repeating mistakes");

        sb.AppendLine("\n═══ Self-Optimization ═══");
        sb.AppendLine("  The prompt system automatically optimizes itself by:");
        sb.AppendLine("  1. Detecting expected tools from user input patterns");
        sb.AppendLine("  2. Comparing actual tool calls in responses");
        sb.AppendLine("  3. Adjusting weights when tools aren't called");
        sb.AppendLine("  4. Adding anti-pattern examples from recent failures");

        return sb.ToString();
    }

    internal static string FormatTimeAgo(DateTime timestamp)
    {
        var elapsed = DateTime.UtcNow - timestamp;
        if (elapsed.TotalSeconds < 60) return "just now";
        if (elapsed.TotalMinutes < 60) return $"{elapsed.TotalMinutes:F0}m ago";
        if (elapsed.TotalHours < 24) return $"{elapsed.TotalHours:F0}h ago";
        return $"{elapsed.TotalDays:F0}d ago";
    }
}
