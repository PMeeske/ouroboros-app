// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using Ouroboros.Agent.MetaAI;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Services;
using Ouroboros.Application.Tools;
using Ouroboros.Pipeline.Council;
using Ouroboros.Pipeline.Learning;
using Ouroboros.Pipeline.Metacognition;
using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.WorldModel;
using Ouroboros.Tools.MeTTa;
using PipelineAgentCapability = Ouroboros.Pipeline.MultiAgent.AgentCapability;

/// <summary>
/// Manages consciousness simulation and AGI cognitive subsystems:
/// learning, metacognition, council debate, world model, and agent coordination.
/// </summary>
public interface ICognitiveSubsystem : IAgentSubsystem
{
    // Consciousness
    ImmersivePersona? ImmersivePersona { get; }

    // Continuous Learning
    ContinuouslyLearningAgent? LearningAgent { get; }
    AdaptiveMetaLearner? MetaLearner { get; }
    ExperienceBuffer? ExperienceBuffer { get; }

    // Metacognition
    RealtimeCognitiveMonitor? CognitiveMonitor { get; }
    BayesianSelfAssessor? SelfAssessor { get; }
    CognitiveIntrospector? Introspector { get; }

    // Council & Coordination
    CouncilOrchestrator? CouncilOrchestrator { get; }
    AgentCoordinator? AgentCoordinator { get; }

    // World Model
    WorldState? WorldState { get; }
}

/// <summary>
/// Cognitive subsystem implementation owning consciousness and AGI components lifecycle.
/// </summary>
public sealed class CognitiveSubsystem : ICognitiveSubsystem
{
    public string Name => "Cognitive";
    public bool IsInitialized { get; private set; }

    // Consciousness
    public ImmersivePersona? ImmersivePersona { get; set; }

    // Continuous Learning
    public ContinuouslyLearningAgent? LearningAgent { get; set; }
    public AdaptiveMetaLearner? MetaLearner { get; set; }
    public ExperienceBuffer? ExperienceBuffer { get; set; }

    // Metacognition
    public RealtimeCognitiveMonitor? CognitiveMonitor { get; set; }
    public BayesianSelfAssessor? SelfAssessor { get; set; }
    public CognitiveIntrospector? Introspector { get; set; }

    // Council & Coordination
    public CouncilOrchestrator? CouncilOrchestrator { get; set; }
    public AgentCoordinator? AgentCoordinator { get; set; }

    // World Model
    public WorldState? WorldState { get; set; }

    public void MarkInitialized() => IsInitialized = true;

    /// <inheritdoc/>
    public async Task InitializeAsync(SubsystemInitContext ctx)
    {
        // ── Orchestrator (Meta-AI planner) ──
        if (ctx.Config.EnableSkills)
            await InitializeOrchestratorCoreAsync(ctx);

        // ── Consciousness (ImmersivePersona) ──
        if (ctx.Config.EnableConsciousness)
            await InitializeConsciousnessCoreAsync(ctx);
        else
            ctx.Output.RecordInit("Consciousness", false, "disabled");

        // ── AGI Subsystems (learning, metacognition, council, world model) ──
        await InitializeAgiSubsystemsCoreAsync(ctx);

        MarkInitialized();
    }

    private async Task InitializeOrchestratorCoreAsync(SubsystemInitContext ctx)
    {
        try
        {
            var chatModel = ctx.Models.ChatModel;
            var embedding = ctx.Models.Embedding;
            var skills = ctx.Memory.Skills;

            if (chatModel != null && embedding != null && skills != null)
            {
                var memory = new MemoryStore(embedding, new TrackedVectorStore());
                var safety = new SafetyGuard();

                var builder = new MetaAIBuilder()
                    .WithLLM(chatModel)
                    .WithTools(ctx.Tools.Tools)
                    .WithEmbedding(embedding)
                    .WithSkillRegistry(skills)
                    .WithSafetyGuard(safety)
                    .WithMemoryStore(memory);

                // Store orchestrator in Autonomy subsystem (it's consumed there)
                ctx.Autonomy.Orchestrator = builder.Build();
                ctx.Output.RecordInit("Orchestrator", true, "Meta-AI planner");
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  \u26a0 Orchestrator unavailable: {ex.Message}");
        }
    }

    private async Task InitializeConsciousnessCoreAsync(SubsystemInitContext ctx)
    {
        try
        {
            ImmersivePersona = new ImmersivePersona(
                ctx.Config.Persona,
                ctx.Memory.MeTTaEngine ?? new InMemoryMeTTaEngine(),
                ctx.Models.Embedding,
                ctx.Config.QdrantEndpoint);

            ImmersivePersona.ConsciousnessShift += (_, e) =>
            {
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.WriteLine($"\n  [consciousness] Emotional shift: {e.NewEmotion} (\u0394 arousal: {e.ArousalChange:+0.00;-0.00})");
                Console.ResetColor();
            };

            await ImmersivePersona.AwakenAsync();
            ctx.Output.RecordInit("Consciousness", true, $"ImmersivePersona '{ctx.Config.Persona}'");

            // Print consciousness state
            var c = ImmersivePersona.Consciousness;
            var sa = ImmersivePersona.SelfAwareness;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"    Emotional state: {c.DominantEmotion} (arousal={c.Arousal:F2}, valence={c.Valence:F2})");
            Console.WriteLine($"    Self-awareness: {sa.Name} - {sa.CurrentMood}");
            Console.WriteLine($"    Identity: {ImmersivePersona.Identity.Name} (uptime: {ImmersivePersona.Uptime:hh\\:mm\\:ss})");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  \u26a0 Consciousness unavailable: {ex.Message}");
        }
    }

    private async Task InitializeAgiSubsystemsCoreAsync(SubsystemInitContext ctx)
    {
        try
        {
            Console.WriteLine("\n  \u2550\u2550\u2550 AGI Subsystems \u2550\u2550\u2550");

            LearningAgent = new ContinuouslyLearningAgent(
                agentId: Guid.NewGuid(),
                config: AdaptiveAgentConfig.Default,
                bufferCapacity: 10000);
            ctx.Output.RecordInit("Continuous Learning", true, "EMA tracking, adaptive strategies");

            MetaLearner = new AdaptiveMetaLearner(explorationWeight: 0.2, historyLimit: 100);
            ctx.Output.RecordInit("Meta-Learner", true, "UCB exploration, Bayesian optimization");

            ExperienceBuffer = new ExperienceBuffer(capacity: 10000);
            ctx.Output.RecordInit("Experience Buffer", true, "10K capacity, prioritized replay");

            CognitiveMonitor = new RealtimeCognitiveMonitor(
                maxBufferSize: 1000,
                slidingWindowDuration: TimeSpan.FromMinutes(5));
            CognitiveMonitor.Subscribe(alert =>
            {
                if (alert.Priority >= 7)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  \u26a0 Cognitive Alert: {alert.Message}");
                    Console.ResetColor();
                }
            });
            ctx.Output.RecordInit("Cognitive Monitor", true, "anomaly detection, health tracking");

            SelfAssessor = new BayesianSelfAssessor();
            ctx.Output.RecordInit("Self-Assessor", true, "6-dimension Bayesian evaluation");

            Introspector = new CognitiveIntrospector(maxHistorySize: 100);
            ctx.Output.RecordInit("Introspector", true, "state capture, pattern detection");

            // Council (needs tool-aware LLM)
            if (ctx.Tools.Llm != null)
            {
                CouncilOrchestrator = CouncilOrchestrator.CreateWithDefaultAgents(ctx.Tools.Llm);
                ctx.Output.RecordInit("Council", true, "5 debate agents, Round Table protocol");
            }

            WorldState = WorldState.Empty();
            ctx.Output.RecordInit("World State", true, "environment tracking, observations");

            // Tool capability matcher + smart selector → stored on ToolSubsystem
            if (ctx.Tools.Tools != null)
            {
                ctx.Tools.ToolCapabilityMatcher = new ToolCapabilityMatcher(ctx.Tools.Tools);
                ctx.Output.RecordInit("Tool Capability Matcher", true, "goal-tool relevance scoring");

                ctx.Tools.SmartToolSelector = new SmartToolSelector(
                    WorldState, ctx.Tools.Tools, ctx.Tools.ToolCapabilityMatcher, SelectionConfig.Default);
                ctx.Output.RecordInit("Smart Tool Selector", true, "balanced optimization strategy");
            }

            // Agent coordinator
            var team = AgentTeam.Empty
                .AddAgent(AgentIdentity.Create("primary", AgentRole.Analyst)
                    .WithCapability(PipelineAgentCapability.Create("reasoning", "Logical reasoning and analysis")))
                .AddAgent(AgentIdentity.Create("critic", AgentRole.Reviewer)
                    .WithCapability(PipelineAgentCapability.Create("evaluation", "Critical evaluation")))
                .AddAgent(AgentIdentity.Create("researcher", AgentRole.Specialist)
                    .WithCapability(PipelineAgentCapability.Create("research", "Information gathering")));
            var messageBus = new InMemoryMessageBus();
            AgentCoordinator = new AgentCoordinator(team, messageBus);
            ctx.Output.RecordInit("Agent Coordinator", true, "3 agents, round-robin delegation");

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  \u26a0 AGI Subsystems: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (ImmersivePersona != null)
            await ImmersivePersona.DisposeAsync();

        IsInitialized = false;
    }
}
