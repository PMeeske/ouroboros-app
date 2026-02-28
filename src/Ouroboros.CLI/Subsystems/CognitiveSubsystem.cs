// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using System.IO;
using Ouroboros.Application.Personality.Consciousness;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Commands;
using Ouroboros.Core.DistinctionLearning;
using Spectre.Console;
using Ouroboros.Domain.DistinctionLearning;
using PipelineAgentCapability = Ouroboros.Pipeline.MultiAgent.AgentCapability;

/// <summary>
/// Cognitive subsystem implementation owning consciousness and AGI components lifecycle.
/// Core orchestrator: fields, initialization, and disposal.
/// Behavioral methods are in partial files:
///   - CognitiveSubsystem.Perception.cs  (consciousness state, emergence, dream, introspect)
///   - CognitiveSubsystem.Reasoning.cs   (learning, cognitive events, council, coordination)
///   - CognitiveSubsystem.Formatting.cs  (AGI status, reports, display helpers)
/// </summary>
public sealed partial class CognitiveSubsystem : ICognitiveSubsystem
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

    // Distinction Learning
    public IDistinctionLearner? DistinctionLearner { get; set; }
    public ConsciousnessDream? Dream { get; set; }
    public DistinctionState CurrentDistinctionState { get; set; } = DistinctionState.Initial();

    // Interconnected Learning
    public InterconnectedLearner? InterconnectedLearner { get; set; }

    // Cross-subsystem context (set during InitializeAsync)
    internal SubsystemInitContext Ctx { get; private set; } = null!;

    //  Runtime cross-subsystem references (set during InitializeAsync)
    internal OuroborosConfig Config { get; private set; } = null!;
    internal IConsoleOutput Output { get; private set; } = null!;
    internal IModelSubsystem Models { get; private set; } = null!;
    internal IToolSubsystem ToolsSub { get; private set; } = null!;
    internal IMemorySubsystem Memory { get; private set; } = null!;
    internal IVoiceSubsystem VoiceService { get; private set; } = null!;
    internal IAutonomySubsystem Autonomy { get; private set; } = null!;

    public void MarkInitialized() => IsInitialized = true;

    /// <inheritdoc/>
    public async Task InitializeAsync(SubsystemInitContext ctx)
    {
        Ctx = ctx;
        Config = ctx.Config;
        Output = ctx.Output;
        Models = ctx.Models;
        ToolsSub = ctx.Tools;
        Memory = ctx.Memory;
        VoiceService = ctx.Voice;
        Autonomy = ctx.Autonomy;

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

        // ── Distinction Learning ──
        InitializeDistinctionLearningCore(ctx);

        // ── Interconnected Learning (tool-skill bridging) ──
        InitializeInterconnectedLearnerCore(ctx);

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
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  ⚠ Orchestrator unavailable: {Markup.Escape(ex.Message)}"));
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
                AnsiConsole.MarkupLine($"\n[rgb(128,0,180)]  [consciousness] Emotional shift: {Markup.Escape(e.NewEmotion ?? "?")} (Δ arousal: {e.ArousalChange:+0.00;-0.00})[/]");
            };

            await ImmersivePersona.AwakenAsync();
            ctx.Output.RecordInit("Consciousness", true, $"ImmersivePersona '{ctx.Config.Persona}'");

            // Print consciousness state
            var c = ImmersivePersona.Consciousness;
            var sa = ImmersivePersona.SelfAwareness;
            AnsiConsole.MarkupLine(OuroborosTheme.Dim($"    Emotional state: {Markup.Escape(c.DominantEmotion)} (arousal={c.Arousal:F2}, valence={c.Valence:F2})"));
            AnsiConsole.MarkupLine(OuroborosTheme.Dim($"    Self-awareness: {Markup.Escape(sa.Name)} - {Markup.Escape(sa.CurrentMood)}"));
            AnsiConsole.MarkupLine(OuroborosTheme.Dim($"    Identity: {Markup.Escape(ImmersivePersona.Identity.Name)} (uptime: {ImmersivePersona.Uptime:hh\\:mm\\:ss})"));
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  ⚠ Consciousness unavailable: {Markup.Escape(ex.Message)}"));
        }
    }

    private async Task InitializeAgiSubsystemsCoreAsync(SubsystemInitContext ctx)
    {
        try
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Accent("\n  ═══ AGI Subsystems ═══"));

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
                    AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  ⚠ Cognitive Alert: {Markup.Escape(alert.Message)}"));
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
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  ⚠ AGI Subsystems: {Markup.Escape(ex.Message)}"));
        }
    }

    private void InitializeDistinctionLearningCore(SubsystemInitContext ctx)
    {
        try
        {
            var storageConfig = DistinctionStorageConfig.Default;
            var storage = new FileSystemDistinctionWeightStorage(storageConfig);
            DistinctionLearner = new DistinctionLearner(storage);
            Dream = new ConsciousnessDream();
            CurrentDistinctionState = DistinctionState.Initial();
            ctx.Output.RecordInit("Distinction Learning", true, "consciousness cycle learning");
        }
        catch (IOException ex)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  ⚠ Distinction Learning: {Markup.Escape(ex.Message)}"));
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  ⚠ Distinction Learning: {Markup.Escape(ex.Message)}"));
        }
    }

    private void InitializeInterconnectedLearnerCore(SubsystemInitContext ctx)
    {
        try
        {
            var embedding = ctx.Models.Embedding;
            if (ctx.Tools.ToolFactory != null && ctx.Memory.Skills != null &&
                ctx.Memory.MeTTaEngine != null && embedding != null && ctx.Tools.Llm != null)
            {
                InterconnectedLearner = new InterconnectedLearner(
                    ctx.Tools.ToolFactory,
                    ctx.Memory.Skills,
                    ctx.Memory.MeTTaEngine,
                    embedding,
                    ctx.Tools.Llm);
                ctx.Output.RecordInit("Interconnected Learning", true, "tool-skill bridging");
            }
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  ⚠ Interconnected Learning: {Markup.Escape(ex.Message)}"));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (ImmersivePersona != null)
            await ImmersivePersona.DisposeAsync();

        IsInitialized = false;
    }
}
