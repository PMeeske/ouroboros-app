// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.
namespace Ouroboros.CLI.Subsystems.Autonomy;

using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.SelfModel;
using Spectre.Console;
using MetaAgentCapability = Ouroboros.Agent.MetaAI.AgentCapability;

/// <summary>
/// Manages the agent's self-model: identity graph, global workspace,
/// predictive monitor, self-evaluator, and capability registry.
/// Extracted from <see cref="AutonomySubsystem"/> to reduce class size.
/// </summary>
internal sealed class SelfModelManager
{
    // ── State ────────────────────────────────────────────────────────────
    public IIdentityGraph? IdentityGraph { get; private set; }
    public IGlobalWorkspace? GlobalWorkspace { get; private set; }
    public IPredictiveMonitor? PredictiveMonitor { get; private set; }
    public ISelfEvaluator? SelfEvaluator { get; private set; }
    public ICapabilityRegistry? CapabilityRegistry { get; private set; }

    /// <summary>
    /// Initializes the self-model subsystem (identity, capabilities, global workspace).
    /// </summary>
    public async Task InitializeCoreAsync(
        SubsystemInitContext ctx,
        MetaAIPlannerOrchestrator? orchestrator)
    {
        try
        {
            var chatModel = ctx.Models.ChatModel;
            if (chatModel == null)
            {
                ctx.Output.RecordInit("Self-Model", false, "requires chat model");
                return;
            }

            CapabilityRegistry = new CapabilityRegistry(chatModel, ctx.Tools.Tools);
            RegisterDefaultCapabilities();

            IdentityGraph = new IdentityGraph(Guid.NewGuid(), ctx.Config.Persona, CapabilityRegistry);
            GlobalWorkspace = new GlobalWorkspace();
            PredictiveMonitor = new PredictiveMonitor();

            if (orchestrator != null && ctx.Memory.Skills != null && ctx.Models.Embedding != null)
            {
                var memory = new MemoryStore(ctx.Models.Embedding, new TrackedVectorStore());
                SelfEvaluator = new SelfEvaluator(
                    chatModel, CapabilityRegistry, ctx.Memory.Skills, memory, orchestrator);
            }

            var capCount = (await CapabilityRegistry.GetCapabilitiesAsync()).Count;
            ctx.Output.RecordInit("Self-Model", true, $"identity graph ({capCount} capabilities)");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn($"SelfModel initialization failed: {ex.Message}")}");
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────────

    private void RegisterDefaultCapabilities()
    {
        if (CapabilityRegistry == null) return;

        RegisterCapability("natural_language",
            "Natural language understanding and generation",
            [], 0.95, 0.5, 100);

        RegisterCapability("planning",
            "Task decomposition and multi-step planning",
            ["orchestrator"], 0.85, 1.0, 50);

        RegisterCapability("tool_use",
            "Dynamic tool creation and invocation",
            [], 0.90, 0.8, 75);

        RegisterCapability("symbolic_reasoning",
            "MeTTa symbolic reasoning and queries",
            ["metta"], 0.80, 0.5, 30);

        RegisterCapability("memory_management",
            "Persistent memory storage and retrieval",
            [], 0.92, 0.3, 60);

        RegisterCapability("pipeline_execution",
            "DSL pipeline construction and execution with reification",
            ["dsl", "network"], 0.88, 0.7, 40);

        RegisterCapability("self_improvement",
            "Autonomous learning, evaluation, and capability enhancement",
            ["evaluator"], 0.75, 2.0, 20);

        RegisterCapability("coding",
            "Code generation, analysis, and debugging",
            [], 0.82, 1.5, 45);
    }

    private void RegisterCapability(
        string name, string description, List<string> requiredTools,
        double successRate, double costFactor, int usageCount)
    {
        CapabilityRegistry!.RegisterCapability(new MetaAgentCapability(
            name, description, requiredTools, successRate, costFactor,
            new List<string>(), usageCount,
            DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, object>()));
    }
}
