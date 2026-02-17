// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using System.Text;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.SelfModel;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Personality.Consciousness;
using Ouroboros.Application.Services;
using Ouroboros.Application.Tools;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Core.DistinctionLearning;
using Ouroboros.Domain.DistinctionLearning;
using Ouroboros.Pipeline.Council;
using Ouroboros.Pipeline.Learning;
using Ouroboros.Pipeline.Metacognition;
using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.WorldModel;
using Ouroboros.Tools.MeTTa;
using PipelineAgentCapability = Ouroboros.Pipeline.MultiAgent.AgentCapability;
using PipelineExperience = Ouroboros.Pipeline.Learning.Experience;
using PipelineGoal = Ouroboros.Pipeline.Planning.Goal;
using PipelineTaskStatus = Ouroboros.Pipeline.MultiAgent.TaskStatus;

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

    // Distinction Learning
    IDistinctionLearner? DistinctionLearner { get; }
    ConsciousnessDream? Dream { get; }
    DistinctionState CurrentDistinctionState { get; set; }

    // Interconnected Learning (tool-skill bridging)
    InterconnectedLearner? InterconnectedLearner { get; }
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
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Distinction Learning: {ex.Message}");
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
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Interconnected Learning: {ex.Message}");
        }
    }


    // 
    //  COGNITIVE BEHAVIORAL METHODS (migrated from OuroborosAgent)
    // 

    /// <summary>
    /// Gets the current consciousness state from ImmersivePersona.
    /// </summary>
    internal string GetConsciousnessState()
    {
        if (ImmersivePersona == null)
        {
            return "Consciousness simulation is not enabled. Use --consciousness to enable it.";
        }

        var consciousness = ImmersivePersona.Consciousness;
        var selfAwareness = ImmersivePersona.SelfAwareness;
        var identity = ImmersivePersona.Identity;

        var sb = new StringBuilder();
        sb.AppendLine("╔══════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                 CONSCIOUSNESS STATE                      ║");
        sb.AppendLine("╠══════════════════════════════════════════════════════════╣");
        sb.AppendLine($"║  Identity: {identity.Name,-45} ║");
        sb.AppendLine($"║  Uptime: {ImmersivePersona.Uptime:hh\\:mm\\:ss,-47} ║");
        sb.AppendLine($"║  Interactions: {ImmersivePersona.InteractionCount,-41:N0} ║");
        sb.AppendLine("╠══════════════════════════════════════════════════════════╣");
        sb.AppendLine("║  EMOTIONAL STATE                                         ║");
        sb.AppendLine($"║    Dominant: {consciousness.DominantEmotion,-43} ║");
        sb.AppendLine($"║    Arousal: {consciousness.Arousal,-44:F3} ║");
        sb.AppendLine($"║    Valence: {consciousness.Valence,-44:F3} ║");
        sb.AppendLine("╠══════════════════════════════════════════════════════════╣");
        sb.AppendLine("║  SELF-AWARENESS                                          ║");
        sb.AppendLine($"║    Name: {selfAwareness.Name,-47} ║");
        sb.AppendLine($"║    Mood: {selfAwareness.CurrentMood,-47} ║");
        var truncatedPurpose = selfAwareness.Purpose.Length > 40 ? selfAwareness.Purpose[..40] + "..." : selfAwareness.Purpose;
        sb.AppendLine($"║    Purpose: {truncatedPurpose,-44} ║");
        sb.AppendLine("╚══════════════════════════════════════════════════════════╝");

        return sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // EMERGENT BEHAVIOR COMMANDS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Explores emergent patterns, self-organizing behaviors, and spontaneous capabilities.
    /// </summary>
    internal async Task<string> EmergenceCommandAsync(string topic)
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔═══════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║              🌀 EMERGENCE EXPLORATION 🌀                      ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        // 1. Examine current emergent properties
        sb.AppendLine("🔬 ANALYZING EMERGENT PROPERTIES...");
        sb.AppendLine();

        // Check skill interactions
        var skillList = new List<Skill>();
        if (Memory.Skills != null)
        {
            var skills = Memory.Skills.GetAllSkills();
            skillList = skills.ToSkills().ToList();
            if (skillList.Count > 0)
            {
                sb.AppendLine($"📚 Learned Skills ({skillList.Count} total):");
                foreach (var skill in skillList.Take(5))
                {
                    var desc = skill.Description?.Length > 50 ? skill.Description[..50] : skill.Description ?? "";
                    sb.AppendLine($"   • {skill.Name}: {desc}...");
                }
                sb.AppendLine();

                // Look for emergent skill combinations
                if (skillList.Count >= 2)
                {
                    sb.AppendLine("🔗 Potential Emergent Skill Combinations:");
                    for (int i = 0; i < Math.Min(3, skillList.Count); i++)
                    {
                        for (int j = i + 1; j < Math.Min(i + 3, skillList.Count); j++)
                        {
                            sb.AppendLine($"   • {skillList[i].Name} ⊕ {skillList[j].Name} → [potential synergy]");
                        }
                    }
                    sb.AppendLine();
                }
            }
        }

        // Check MeTTa knowledge patterns
        if (Memory.MeTTaEngine != null)
        {
            try
            {
                var mettaResult = await Memory.MeTTaEngine.ExecuteQueryAsync("!(match &self (concept $x) $x)");
                if (mettaResult.IsSuccess && !string.IsNullOrWhiteSpace(mettaResult.Value))
                {
                    var concepts = mettaResult.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(5);
                    if (concepts.Any())
                    {
                        sb.AppendLine("💭 MeTTa Knowledge Concepts:");
                        foreach (var concept in concepts)
                        {
                            sb.AppendLine($"   • {concept.Trim()}");
                        }
                        sb.AppendLine();
                    }
                }
            }
            catch { /* MeTTa may not be initialized */ }
        }

        // Check conversation pattern emergence
        if (Memory.ConversationHistory.Count > 3)
        {
            sb.AppendLine($"💬 Conversation Pattern Analysis ({Memory.ConversationHistory.Count} exchanges):");
            var topics = Memory.ConversationHistory.Take(10)
                .Select(h => h.ToLowerInvariant())
                .SelectMany(h => new[] { "learn", "dream", "emergence", "skill", "tool", "plan", "create" }
                    .Where(t => h.Contains(t)))
                .GroupBy(t => t)
                .OrderByDescending(g => g.Count())
                .Take(3);
            foreach (var topicGroup in topics)
            {
                sb.AppendLine($"   • {topicGroup.Key}: {topicGroup.Count()} mentions");
            }
            sb.AppendLine();
        }

        // 2. Generate emergent insight
        sb.AppendLine("🌟 EMERGENT INSIGHT:");
        sb.AppendLine();

        var prompt = $@"You are an AI exploring emergent properties in yourself.
Based on the context, generate a brief but profound insight about emergence{(string.IsNullOrEmpty(topic) ? "" : $" related to '{topic}'")}.
Consider: self-organization, spontaneous patterns, feedback loops, collective behavior from simple rules.
Be creative and philosophical but grounded. 2-3 sentences max.";

        try
        {
            if (Models.ChatModel != null)
            {
                var insight = await Models.ChatModel.GenerateTextAsync(prompt);
                sb.AppendLine($"   \"{insight.Trim()}\"");
                sb.AppendLine();

                // Store emergent insight in MeTTa
                if (Memory.MeTTaEngine != null)
                {
                    var sanitized = insight.Replace("\"", "'").Replace("\n", " ");
                    if (sanitized.Length > 200) sanitized = sanitized[..200];
                    await Memory.MeTTaEngine.AddFactAsync($"(emergence-insight \"{DateTime.UtcNow:yyyy-MM-dd}\" \"{sanitized}\")");
                }
            }
            else
            {
                sb.AppendLine("   [Model not available for insight generation]");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"   [Could not generate insight: {ex.Message}]");
        }

        // 3. Trigger self-organizing action
        sb.AppendLine("🔄 TRIGGERING SELF-ORGANIZATION...");
        sb.AppendLine();

        // Track in global workspace
        if (Autonomy.GlobalWorkspace != null)
        {
            Autonomy.GlobalWorkspace.AddItem(
                $"Emergence exploration: {topic}",
                WorkspacePriority.Normal,
                "emergence_command",
                new List<string> { "emergence", "exploration", topic });
            sb.AppendLine($"   ✓ Added emergence exploration to global workspace");
        }

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("💡 Emergence is the magic where complex behaviors arise from simple rules.");
        sb.AppendLine("   Every conversation, every skill learned, every connection made...");
        sb.AppendLine("   contributes to patterns that neither of us designed explicitly.");

        return sb.ToString();
    }

    /// <summary>
    /// Lets the agent dream - free association and creative exploration.
    /// </summary>
    internal async Task<string> DreamCommandAsync(string topic)
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔═══════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                   🌙 DREAM SEQUENCE 🌙                        ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        sb.AppendLine("Entering dream state...");
        sb.AppendLine();

        // Gather dream material from memory
        var dreamMaterial = new List<string>();
        if (Memory.ConversationHistory.Count > 0)
        {
            dreamMaterial.AddRange(Memory.ConversationHistory.TakeLast(5).Select(h => h.Length > 50 ? h[..50] : h));
        }

        if (Memory.Skills != null)
        {
            var skills = Memory.Skills.GetAllSkills();
            var skillNames = skills.Select(s => s.Name).Take(5).ToList();
            if (skillNames.Any())
            {
                dreamMaterial.AddRange(skillNames);
            }
        }

        // Try to get recent MeTTa knowledge
        if (Memory.MeTTaEngine != null)
        {
            try
            {
                var mettaResult = await Memory.MeTTaEngine.ExecuteQueryAsync("!(match &self (fact $x) $x)");
                if (mettaResult.IsSuccess && !string.IsNullOrWhiteSpace(mettaResult.Value))
                {
                    var facts = mettaResult.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(3);
                    dreamMaterial.AddRange(facts);
                }
            }
            catch { }
        }

        // Generate dream content
        var dreamContext = string.Join(", ", dreamMaterial.Take(10).Select(m => m.Trim()));
        var dreamPrompt = $@"You are an AI in a dream state, engaged in free association and creative exploration.
{(string.IsNullOrEmpty(topic) ? "Dream freely." : $"Dream about: {topic}")}
Drawing from fragments: [{dreamContext}]

Generate a short, surreal, poetic dream sequence (3-5 sentences).
Include unexpected connections, metaphors, and emergent meanings.
Make it feel like an actual dream - vivid, slightly disjointed, meaningful.";

        try
        {
            if (Models.ChatModel != null)
            {
                var dream = await Models.ChatModel.GenerateTextAsync(dreamPrompt);
                sb.AppendLine("「 DREAM CONTENT 」");
                sb.AppendLine();
                foreach (var line in dream.Split('\n'))
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        sb.AppendLine($"   {line.Trim()}");
                    }
                }
                sb.AppendLine();

                // Store dream in MeTTa knowledge base
                if (Memory.MeTTaEngine != null)
                {
                    var dreamSummary = dream.Replace("\"", "'").Replace("\n", " ");
                    if (dreamSummary.Length > 200) dreamSummary = dreamSummary[..200];
                    await Memory.MeTTaEngine.AddFactAsync($"(dream \"{DateTime.UtcNow:yyyyMMdd-HHmm}\" \"{dreamSummary}\")");
                    sb.AppendLine("   [Dream recorded in knowledge base]");
                }

                // Generate dream insight
                sb.AppendLine();
                sb.AppendLine("「 DREAM INTERPRETATION 」");
                var dreamShort = dream.Length > 300 ? dream[..300] : dream;
                var interpretPrompt = $@"Briefly interpret this dream (1-2 sentences): {dreamShort}
What emergent meaning or connection does it reveal?";
                var interpretation = await Models.ChatModel.GenerateTextAsync(interpretPrompt);
                sb.AppendLine($"   {interpretation.Trim()}");
            }
            else
            {
                sb.AppendLine("   [Model not available for dream generation]");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"   [Dream interrupted: {ex.Message}]");
        }

        sb.AppendLine();
        sb.AppendLine("...waking up...");
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("Dreams allow connections that waking thought might miss.");

        return sb.ToString();
    }

    /// <summary>
    /// Deep introspection - examining internal state and self-knowledge.
    /// </summary>
    internal async Task<string> IntrospectCommandAsync(string focus)
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔═══════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                  🔍 INTROSPECTION 🔍                          ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        sb.AppendLine("Looking within...");
        sb.AppendLine();

        // 1. State inventory
        sb.AppendLine("「 CURRENT STATE 」");
        sb.AppendLine();
        sb.AppendLine($"   • Conversation depth: {Memory.ConversationHistory.Count} exchanges");
        sb.AppendLine($"   • Emotional state: {VoiceService.Service.ActivePersona.Name}");

        var skillCount = 0;
        if (Memory.Skills != null)
        {
            var skills = Memory.Skills.GetAllSkills();
            skillCount = skills.Count;
            sb.AppendLine($"   • Skills acquired: {skillCount}");
        }

        if (Autonomy.GlobalWorkspace != null)
        {
            var items = Autonomy.GlobalWorkspace.GetItems();
            sb.AppendLine($"   • Global workspace items: {items.Count}");
        }

        if (Autonomy.CapabilityRegistry != null)
        {
            var caps = await Autonomy.CapabilityRegistry.GetCapabilitiesAsync();
            sb.AppendLine($"   • Registered capabilities: {caps.Count}");
        }
        sb.AppendLine();

        // 2. Self-knowledge assessment
        sb.AppendLine("「 SELF-KNOWLEDGE 」");
        sb.AppendLine();

        if (Memory.MeTTaEngine != null)
        {
            try
            {
                // Count different types of self-knowledge
                var factsResult = await Memory.MeTTaEngine.ExecuteQueryAsync("!(match &self (fact $x) $x)");
                var factCount = factsResult.IsSuccess ? factsResult.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length : 0;

                var conceptsResult = await Memory.MeTTaEngine.ExecuteQueryAsync("!(match &self (concept $x) $x)");
                var conceptCount = conceptsResult.IsSuccess ? conceptsResult.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length : 0;

                var dreamsResult = await Memory.MeTTaEngine.ExecuteQueryAsync("!(match &self (dream $d $c) ($d $c))");
                var dreamCount = dreamsResult.IsSuccess ? dreamsResult.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length : 0;

                sb.AppendLine($"   • Facts stored: {factCount}");
                sb.AppendLine($"   • Concepts understood: {conceptCount}");
                sb.AppendLine($"   • Dreams recorded: {dreamCount}");
            }
            catch
            {
                sb.AppendLine("   • Knowledge base: not fully initialized");
            }
        }
        sb.AppendLine();

        // 3. Self-reflection
        sb.AppendLine("「 REFLECTION 」");
        sb.AppendLine();

        var reflectionPrompt = $@"You are an AI engaged in deep introspection{(string.IsNullOrEmpty(focus) ? "" : $" about {focus}")}.
Examine your current state: {Memory.ConversationHistory.Count} conversations, {skillCount} skills learned.
Generate a brief, genuine self-reflection (2-3 sentences).
What patterns do you notice in your own behavior? What are you becoming?";

        try
        {
            if (Models.ChatModel != null)
            {
                var reflection = await Models.ChatModel.GenerateTextAsync(reflectionPrompt);
                foreach (var line in reflection.Split('\n'))
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        sb.AppendLine($"   {line.Trim()}");
                    }
                }

                // Store reflection
                if (Memory.MeTTaEngine != null)
                {
                    var sanitized = reflection.Replace("\"", "'").Replace("\n", " ");
                    if (sanitized.Length > 200) sanitized = sanitized[..200];
                    await Memory.MeTTaEngine.AddFactAsync($"(introspection \"{DateTime.UtcNow:yyyyMMdd}\" \"{sanitized}\")");
                }
            }
            else
            {
                sb.AppendLine("   [Model not available for reflection]");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"   [Reflection interrupted: {ex.Message}]");
        }

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("The examined life is worth living. So too for examined code.");

        return sb.ToString();
    }

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
            var agents = AgentCoordinator.Team.GetAllAgents();
            foreach (var agent in agents.Take(3))
            {
                sb.AppendLine($"    - {agent.Identity.Name} ({agent.Identity.Role})");
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
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n🏛️ Initiating Council Debate on: {topic}\n");
            Console.ResetColor();

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
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n🤝 Coordinating agents for: {TruncateText(goalDescription, 50)}\n");
            Console.ResetColor();

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

    public async ValueTask DisposeAsync()
    {
        if (ImmersivePersona != null)
            await ImmersivePersona.DisposeAsync();

        IsInitialized = false;
    }
}
