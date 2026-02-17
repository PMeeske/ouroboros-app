// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using System.Collections.Concurrent;
using Ouroboros.Abstractions.Agent;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.SelfModel;
using Ouroboros.Application.SelfAssembly;
using Ouroboros.Application.Services;
using Ouroboros.Application.Tools;
using Ouroboros.CLI.Commands;
using Ouroboros.Network;
using Ouroboros.Tools.MeTTa;
using static Ouroboros.Application.Tools.AutonomousTools;
using MetaAgentCapability = Ouroboros.Agent.MetaAI.AgentCapability;
using MetaAgentStatus = Ouroboros.Agent.MetaAI.AgentStatus;

/// <summary>
/// Manages autonomous behavior: autonomous mind, coordinator, self-execution,
/// sub-agent orchestration, self-assembly, self-indexing, and network state.
/// </summary>
public interface IAutonomySubsystem : IAgentSubsystem
{
    // Autonomous Mind
    AutonomousMind? AutonomousMind { get; }
    AutonomousCoordinator? Coordinator { get; }
    MetaAIPlannerOrchestrator? Orchestrator { get; }

    // Self-Execution
    ConcurrentQueue<AutonomousGoal> GoalQueue { get; }
    bool SelfExecutionEnabled { get; set; }

    // Sub-Agent Orchestration
    ConcurrentDictionary<string, SubAgentInstance> SubAgents { get; }
    IDistributedOrchestrator? DistributedOrchestrator { get; }
    IEpicBranchOrchestrator? EpicOrchestrator { get; }

    // Self-Model
    IIdentityGraph? IdentityGraph { get; }
    IGlobalWorkspace? GlobalWorkspace { get; }
    IPredictiveMonitor? PredictiveMonitor { get; }
    ISelfEvaluator? SelfEvaluator { get; }
    ICapabilityRegistry? CapabilityRegistry { get; }

    // Self-Assembly
    SelfAssemblyEngine? SelfAssemblyEngine { get; }
    BlueprintAnalyzer? BlueprintAnalyzer { get; }
    MeTTaBlueprintValidator? BlueprintValidator { get; }

    // Self-Code Perception
    QdrantSelfIndexer? SelfIndexer { get; }

    // Network State
    NetworkStateTracker? NetworkTracker { get; }
}

/// <summary>
/// Autonomy subsystem implementation owning all autonomous behavior and self-management.
/// </summary>
public sealed class AutonomySubsystem : IAutonomySubsystem
{
    public string Name => "Autonomy";
    public bool IsInitialized { get; private set; }

    // Autonomous Mind
    public AutonomousMind? AutonomousMind { get; set; }
    public AutonomousCoordinator? Coordinator { get; set; }
    public MetaAIPlannerOrchestrator? Orchestrator { get; set; }

    // Self-Execution
    public ConcurrentQueue<AutonomousGoal> GoalQueue { get; } = new();
    public Task? SelfExecutionTask { get; set; }
    public CancellationTokenSource? SelfExecutionCts { get; set; }
    public bool SelfExecutionEnabled { get; set; }

    // Sub-Agent Orchestration
    public ConcurrentDictionary<string, SubAgentInstance> SubAgents { get; } = new();
    public IDistributedOrchestrator? DistributedOrchestrator { get; set; }
    public IEpicBranchOrchestrator? EpicOrchestrator { get; set; }

    // Self-Model
    public IIdentityGraph? IdentityGraph { get; set; }
    public IGlobalWorkspace? GlobalWorkspace { get; set; }
    public IPredictiveMonitor? PredictiveMonitor { get; set; }
    public ISelfEvaluator? SelfEvaluator { get; set; }
    public ICapabilityRegistry? CapabilityRegistry { get; set; }

    // Self-Assembly
    public SelfAssemblyEngine? SelfAssemblyEngine { get; set; }
    public BlueprintAnalyzer? BlueprintAnalyzer { get; set; }
    public MeTTaBlueprintValidator? BlueprintValidator { get; set; }

    // Self-Code Perception
    public QdrantSelfIndexer? SelfIndexer { get; set; }

    // Network State
    public NetworkStateTracker? NetworkTracker { get; set; }

    // Push Mode
    public Task? PushModeTask { get; set; }
    public CancellationTokenSource? PushModeCts { get; set; }

    public void MarkInitialized() => IsInitialized = true;

    /// <inheritdoc/>
    public async Task InitializeAsync(SubsystemInitContext ctx)
    {
        // ── Autonomous Mind (core creation; delegate wiring done by agent mediator) ──
        if (ctx.Config.EnableMind)
        {
            AutonomousMind = new AutonomousMind();
            if (!string.IsNullOrEmpty(ctx.Config.Culture))
                AutonomousMind.Culture = ctx.Config.Culture;
            ctx.Output.RecordInit("Autonomous Mind", true, "core created (delegates pending)");
        }
        else
        {
            ctx.Output.RecordInit("Autonomous Mind", false, "disabled");
        }

        // ── Sub-Agent Orchestration ──
        await InitializeSubAgentOrchestrationCoreAsync(ctx);

        // ── Self-Model (identity, capabilities, global workspace) ──
        await InitializeSelfModelCoreAsync(ctx);

        // ── Network State (Merkle-DAG + Qdrant) ──
        await InitializeNetworkStateCoreAsync(ctx);

        // ── Self-Indexer (code perception) ──
        await InitializeSelfIndexerCoreAsync(ctx);

        // ── Self-Assembly ──
        await InitializeSelfAssemblyCoreAsync(ctx);

        // ── Self-Execution (background goal pursuit) ──
        // NOTE: SelfExecution loop references agent methods → wired by mediator

        MarkInitialized();
    }

    private async Task InitializeSubAgentOrchestrationCoreAsync(SubsystemInitContext ctx)
    {
        try
        {
            var safety = new SafetyGuard();
            DistributedOrchestrator = new DistributedOrchestrator(safety);

            var selfCaps = new HashSet<string>
            {
                "planning", "reasoning", "coding", "research", "analysis",
                "summarization", "tool_use", "metta_reasoning"
            };
            var selfAgent = new AgentInfo(
                "ouroboros-primary", ctx.Config.Persona, selfCaps,
                MetaAgentStatus.Available, DateTime.UtcNow);
            DistributedOrchestrator.RegisterAgent(selfAgent);

            EpicOrchestrator = new EpicBranchOrchestrator(
                DistributedOrchestrator,
                new EpicBranchConfig(
                    BranchPrefix: "ouroboros-epic",
                    AgentPoolPrefix: "sub-agent",
                    AutoCreateBranches: true,
                    AutoAssignAgents: true,
                    MaxConcurrentSubIssues: 5));

            ctx.Output.RecordInit("Sub-Agents", true, "distributed orchestration (1 agent)");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  \u26a0 SubAgent orchestration failed: {ex.Message}");
        }
    }

    private async Task InitializeSelfModelCoreAsync(SubsystemInitContext ctx)
    {
        try
        {
            var chatModel = ctx.Models.ChatModel;
            if (chatModel != null)
            {
                CapabilityRegistry = new CapabilityRegistry(chatModel, ctx.Tools.Tools);

                CapabilityRegistry.RegisterCapability(new MetaAgentCapability(
                    "natural_language", "Natural language understanding and generation",
                    new List<string>(), 0.95, 0.5, new List<string>(), 100,
                    DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, object>()));
                CapabilityRegistry.RegisterCapability(new MetaAgentCapability(
                    "planning", "Task decomposition and multi-step planning",
                    new List<string> { "orchestrator" }, 0.85, 1.0, new List<string>(), 50,
                    DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, object>()));
                CapabilityRegistry.RegisterCapability(new MetaAgentCapability(
                    "tool_use", "Dynamic tool creation and invocation",
                    new List<string>(), 0.90, 0.8, new List<string>(), 75,
                    DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, object>()));
                CapabilityRegistry.RegisterCapability(new MetaAgentCapability(
                    "symbolic_reasoning", "MeTTa symbolic reasoning and queries",
                    new List<string> { "metta" }, 0.80, 0.5, new List<string>(), 30,
                    DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, object>()));
                CapabilityRegistry.RegisterCapability(new MetaAgentCapability(
                    "memory_management", "Persistent memory storage and retrieval",
                    new List<string>(), 0.92, 0.3, new List<string>(), 60,
                    DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, object>()));
                CapabilityRegistry.RegisterCapability(new MetaAgentCapability(
                    "pipeline_execution", "DSL pipeline construction and execution with reification",
                    new List<string> { "dsl", "network" }, 0.88, 0.7, new List<string>(), 40,
                    DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, object>()));
                CapabilityRegistry.RegisterCapability(new MetaAgentCapability(
                    "self_improvement", "Autonomous learning, evaluation, and capability enhancement",
                    new List<string> { "evaluator" }, 0.75, 2.0, new List<string>(), 20,
                    DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, object>()));
                CapabilityRegistry.RegisterCapability(new MetaAgentCapability(
                    "coding", "Code generation, analysis, and debugging",
                    new List<string>(), 0.82, 1.5, new List<string>(), 45,
                    DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, object>()));

                IdentityGraph = new IdentityGraph(Guid.NewGuid(), ctx.Config.Persona, CapabilityRegistry);
                GlobalWorkspace = new GlobalWorkspace();
                PredictiveMonitor = new PredictiveMonitor();

                if (Orchestrator != null && ctx.Memory.Skills != null && ctx.Models.Embedding != null)
                {
                    var memory = new MemoryStore(ctx.Models.Embedding, new TrackedVectorStore());
                    SelfEvaluator = new SelfEvaluator(
                        chatModel, CapabilityRegistry, ctx.Memory.Skills, memory, Orchestrator);
                }

                var capCount = (await CapabilityRegistry.GetCapabilitiesAsync()).Count;
                ctx.Output.RecordInit("Self-Model", true, $"identity graph ({capCount} capabilities)");
            }
            else
            {
                ctx.Output.RecordInit("Self-Model", false, "requires chat model");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  \u26a0 SelfModel initialization failed: {ex.Message}");
        }
    }

    private async Task InitializeNetworkStateCoreAsync(SubsystemInitContext ctx)
    {
        try
        {
            NetworkTracker = new NetworkStateTracker();

            if (!string.IsNullOrEmpty(ctx.Config.QdrantEndpoint))
            {
                try
                {
                    Func<string, Task<float[]>>? embeddingFunc = null;
                    if (ctx.Models.Embedding != null)
                        embeddingFunc = async (text) => await ctx.Models.Embedding.CreateEmbeddingsAsync(text);

                    var dagConfig = new Ouroboros.Network.QdrantDagConfig(
                        Endpoint: ctx.Config.QdrantEndpoint,
                        NodesCollection: "ouroboros_dag_nodes",
                        EdgesCollection: "ouroboros_dag_edges",
                        VectorSize: 768);
                    var dagStore = new Ouroboros.Network.QdrantDagStore(dagConfig, embeddingFunc);
                    await dagStore.InitializeAsync();
                    NetworkTracker.ConfigureQdrantPersistence(dagStore, autoPersist: true);
                    ctx.Output.RecordInit("Network State", true, "Merkle-DAG with Qdrant persistence");
                }
                catch (Exception qdrantEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[NetworkState] Qdrant DAG storage unavailable: {qdrantEx.Message}");
                    ctx.Output.RecordInit("Network State", true, "Merkle-DAG (in-memory)");
                }
            }
            else
            {
                ctx.Output.RecordInit("Network State", true, "Merkle-DAG (in-memory)");
            }

            if (ctx.Memory.MeTTaEngine != null)
            {
                NetworkTracker.ConfigureMeTTaExport(ctx.Memory.MeTTaEngine, autoExport: true);
                Console.WriteLine("    \u2713 MeTTa symbolic export enabled (DAG facts \u2192 MeTTa)");
            }

            NetworkTracker.BranchReified += (_, args) =>
            {
                System.Diagnostics.Debug.WriteLine($"[NetworkState] Branch '{args.BranchName}' reified: {args.NodesCreated} nodes");
            };

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  \u26a0 NetworkState initialization failed: {ex.Message}");
            NetworkTracker = new NetworkStateTracker();
        }
    }

    private async Task InitializeSelfIndexerCoreAsync(SubsystemInitContext ctx)
    {
        if (ctx.Models.Embedding == null)
        {
            ctx.Output.RecordInit("Self-Index", false, "no embedding model");
            return;
        }

        try
        {
            var currentDir = AppContext.BaseDirectory;
            var workspaceRoot = currentDir;
            for (int i = 0; i < 6; i++)
            {
                var parent = Directory.GetParent(workspaceRoot);
                if (parent == null) break;
                workspaceRoot = parent.FullName;
                if (Directory.GetFiles(workspaceRoot, "*.sln").Length > 0 ||
                    Directory.Exists(Path.Combine(workspaceRoot, "src")))
                    break;
            }

            var indexerConfig = new QdrantIndexerConfig
            {
                QdrantEndpoint = ctx.Config.QdrantEndpoint,
                CollectionName = "ouroboros_selfindex",
                HashCollectionName = "ouroboros_filehashes",
                RootPaths = new List<string> { Path.Combine(workspaceRoot, "src") },
                EnableFileWatcher = true,
                ChunkSize = 800,
                ChunkOverlap = 150
            };

            SelfIndexer = new QdrantSelfIndexer(ctx.Models.Embedding, indexerConfig);
            SelfIndexer.OnFileIndexed += (file, chunks) =>
            {
                System.Diagnostics.Debug.WriteLine($"[SelfIndex] {Path.GetFileName(file)}: {chunks} chunks");
            };

            await SelfIndexer.InitializeAsync();
            SystemAccessTools.SharedIndexer = SelfIndexer;

            var stats = await SelfIndexer.GetStatsAsync();
            ctx.Output.RecordInit("Self-Index", true, $"{stats.IndexedFiles} files, {stats.TotalVectors} chunks");

            _ = Task.Run(async () =>
            {
                try
                {
                    var progress = await SelfIndexer.IncrementalIndexAsync();
                    if (progress.ProcessedFiles > 0)
                        System.Diagnostics.Debug.WriteLine($"[SelfIndex] Incremental: {progress.ProcessedFiles} files, {progress.IndexedChunks} chunks");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SelfIndex] Incremental failed: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  \u26a0 SelfIndex: {ex.Message}");
            SelfIndexer = null;
        }
    }

    private async Task InitializeSelfAssemblyCoreAsync(SubsystemInitContext ctx)
    {
        try
        {
            var config = new SelfAssemblyConfig
            {
                AutoApprovalEnabled = ctx.Config.YoloMode,
                AutoApprovalThreshold = 0.95,
                MinSafetyScore = 0.8,
                MaxAssembledNeurons = 10,
                ForbiddenCapabilities = new HashSet<NeuronCapability>
                {
                    NeuronCapability.FileAccess,
                },
                SandboxTimeout = TimeSpan.FromSeconds(30),
            };

            SelfAssemblyEngine = new SelfAssemblyEngine(config);

            // MeTTa validation
            BlueprintValidator = new MeTTaBlueprintValidator();
            if (ctx.Memory.MeTTaEngine != null)
            {
                BlueprintValidator.MeTTaExecutor = async (expr, ct) =>
                {
                    try
                    {
                        var result = await ctx.Memory.MeTTaEngine.ExecuteQueryAsync(expr, ct);
                        return result.Match(s => s, e => "False");
                    }
                    catch { return "False"; }
                };
            }

            SelfAssemblyEngine.SetMeTTaValidator(async blueprint =>
                await BlueprintValidator.ValidateAsync(blueprint));

            // LLM code generation + approval callback → wired by agent mediator

            // Blueprint analyzer
            if (Coordinator?.Network != null)
            {
                BlueprintAnalyzer = new BlueprintAnalyzer(Coordinator.Network);
                if (ctx.Tools.Llm != null)
                {
                    BlueprintAnalyzer.LlmAnalyzer = async (prompt, ct) =>
                        await ctx.Tools.Llm.InnerModel.GenerateTextAsync(prompt, ct);
                }
            }

            ctx.Output.RecordInit("Self-Assembly", true, $"YOLO={ctx.Config.YoloMode}, max {config.MaxAssembledNeurons} neurons");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  \u26a0 SelfAssembly: {ex.Message}");
            SelfAssemblyEngine = null;
        }

        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        // Stop self-execution
        SelfExecutionEnabled = false;
        SelfExecutionCts?.Cancel();
        if (SelfExecutionTask != null)
        {
            try { await SelfExecutionTask; } catch { /* ignored */ }
        }
        SelfExecutionCts?.Dispose();

        // Stop push mode
        PushModeCts?.Cancel();
        if (PushModeTask != null)
        {
            try { await PushModeTask; } catch { /* ignored */ }
        }
        PushModeCts?.Dispose();

        // Clear sub-agents
        SubAgents.Clear();

        // Dispose autonomous mind
        AutonomousMind?.Dispose();

        // Dispose self-indexer (stops file watchers)
        if (SelfIndexer != null)
            await SelfIndexer.DisposeAsync();

        // Dispose self-assembly engine (stops assembled neurons)
        if (SelfAssemblyEngine != null)
            await SelfAssemblyEngine.DisposeAsync();

        // Dispose network tracker
        NetworkTracker?.Dispose();

        IsInitialized = false;
    }
}
