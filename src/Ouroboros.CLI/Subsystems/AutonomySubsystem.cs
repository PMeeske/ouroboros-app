// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using System.Collections.Concurrent;
using LangChain.DocumentLoaders;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Abstractions.Monads;
using Unit = Ouroboros.Abstractions.Unit;
using PipelineReasoningStep = Ouroboros.Domain.Events.ReasoningStep;
using System.Text;
using System.Text.RegularExpressions;
using Ouroboros.Abstractions.Agent;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.SelfModel;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Personality.Consciousness;
using Ouroboros.Application.SelfAssembly;
using Ouroboros.Application.Services;
using Ouroboros.Application.Tools;
using Ouroboros.CLI.Commands;
using Ouroboros.Core.Configuration;
using Ouroboros.Domain.Voice;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Network;
using Ouroboros.Tools.MeTTa;
using Qdrant.Client;
using static Ouroboros.Application.Tools.AutonomousTools;
using MetaAgentCapability = Ouroboros.Agent.MetaAI.AgentCapability;
using MetaAgentStatus = Ouroboros.Agent.MetaAI.AgentStatus;

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

    // Persistent Network State Projector
    public PersistentNetworkStateProjector? NetworkProjector { get; set; }

    // Push Mode
    public Task? PushModeTask { get; set; }
    public CancellationTokenSource? PushModeCts { get; set; }

    // ── Runtime cross-subsystem references (set during InitializeAsync) ──
    internal OuroborosConfig Config { get; private set; } = null!;
    internal IConsoleOutput Output { get; private set; } = null!;
    internal ModelSubsystem Models { get; private set; } = null!;
    internal ToolSubsystem Tools { get; private set; } = null!;
    internal MemorySubsystem Memory { get; private set; } = null!;
    internal VoiceSubsystem Voice { get; private set; } = null!;
    internal CognitiveSubsystem Cognitive { get; private set; } = null!;

    // ── Agent-level callbacks (wired by mediator after init) ──
    internal Func<bool> IsInConversationLoop { get; set; } = () => false;
    internal Func<string, string?, Task> SayAndWaitAsyncFunc { get; set; } = (_, _) => Task.CompletedTask;
    internal Action<string> AnnounceAction { get; set; } = _ => { };
    internal Func<string, Task<string>> ChatAsyncFunc { get; set; } = _ => Task.FromResult("");
    internal Func<string, string> GetLanguageNameFunc { get; set; } = _ => "English";
    internal Func<Task> StartListeningAsyncFunc { get; set; } = () => Task.CompletedTask;
    internal Action StopListeningAction { get; set; } = () => { };

    public void MarkInitialized() => IsInitialized = true;

    /// <inheritdoc/>
    public async Task InitializeAsync(SubsystemInitContext ctx)
    {
        // Store cross-subsystem references for runtime use
        Config = ctx.Config;
        Output = ctx.Output;
        Models = ctx.Models;
        Tools = ctx.Tools;
        Memory = ctx.Memory;
        Voice = ctx.Voice;
        Cognitive = ctx.Cognitive;
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

        // ── Persistent Network State Projector (learning persistence) ──
        await InitializeNetworkProjectorCoreAsync(ctx);

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

            var dagClient = ctx.Services?.GetService<QdrantClient>();
            var dagRegistry = ctx.Services?.GetService<IQdrantCollectionRegistry>();
            if (dagClient != null && dagRegistry != null)
            {
                try
                {
                    Func<string, Task<float[]>>? embeddingFunc = null;
                    if (ctx.Models.Embedding != null)
                        embeddingFunc = async (text) => await ctx.Models.Embedding.CreateEmbeddingsAsync(text);

                    var dagStore = new Ouroboros.Network.QdrantDagStore(dagClient, dagRegistry, embeddingFunc);
                    await dagStore.InitializeAsync();
                    NetworkTracker.ConfigureQdrantPersistence(dagStore, autoPersist: true);
                    ctx.Output.RecordInit("Network State", true, "Merkle-DAG with Qdrant persistence (DI)");
                }
                catch (Exception qdrantEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[NetworkState] Qdrant DAG storage unavailable: {qdrantEx.Message}");
                    ctx.Output.RecordInit("Network State", true, "Merkle-DAG (in-memory)");
                }
            }
            else if (!string.IsNullOrEmpty(ctx.Config.QdrantEndpoint))
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

    private async Task InitializeNetworkProjectorCoreAsync(SubsystemInitContext ctx)
    {
        var embedding = ctx.Models.Embedding;
        if (embedding == null) return;

        try
        {
            var dag = new Ouroboros.Network.MerkleDag();
            Func<string, Task<float[]>> embedFunc = async text => await embedding.CreateEmbeddingsAsync(text);
            var npClient = ctx.Services?.GetService<QdrantClient>();
            var npRegistry = ctx.Services?.GetService<IQdrantCollectionRegistry>();
            var npSettings = ctx.Services?.GetService<QdrantSettings>();
            if (npClient != null && npRegistry != null && npSettings != null)
                NetworkProjector = new PersistentNetworkStateProjector(
                    dag, npClient, npRegistry, npSettings, embedFunc);
            else
            {
                var qdrantEndpoint = NormalizeEndpoint(ctx.Config.QdrantEndpoint, "http://localhost:6334");
                NetworkProjector = new PersistentNetworkStateProjector(
                    dag, qdrantEndpoint, embedFunc);
            }
            await NetworkProjector.InitializeAsync(CancellationToken.None);
            ctx.Output.RecordInit("Network Projector", true,
                $"epoch {NetworkProjector.CurrentEpoch}, {NetworkProjector.RecentLearnings.Count} learnings");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Network Projector: {ex.Message}");
        }
    }

    private static string NormalizeEndpoint(string? rawEndpoint, string fallbackEndpoint)
    {
        var endpoint = (rawEndpoint ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return fallbackEndpoint;
        }

        if (!endpoint.Contains("://", StringComparison.Ordinal))
        {
            endpoint = $"http://{endpoint}";
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
        {
            return fallbackEndpoint;
        }

        return uri.ToString().TrimEnd('/');
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

            var siClient = ctx.Services?.GetService<QdrantClient>();
            var siRegistry = ctx.Services?.GetService<IQdrantCollectionRegistry>();
            if (siClient != null && siRegistry != null)
                SelfIndexer = new QdrantSelfIndexer(siClient, siRegistry, ctx.Models.Embedding, indexerConfig);
            else
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


    // 
    // MIGRATED FROM OuroborosAgent  Autonomous behavior methods
    // 

    /// <summary>
    /// Initializes the autonomous coordinator (always enabled for status, commands, network).
    /// </summary>
    internal async Task InitializeAutonomousCoordinatorAsync()
    {
        try
        {
            // Parse auto-approve categories from config
            HashSet<string> autoApproveCategories = Config.AutoApproveCategories
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Create autonomous configuration using existing API
            AutonomousConfiguration autonomousConfig = new AutonomousConfiguration
            {
                PushBasedMode = Config.EnablePush,
                YoloMode = Config.YoloMode,
                TickIntervalSeconds = Config.IntentionIntervalSeconds,
                AutoApproveLowRisk = autoApproveCategories.Contains("safe") || autoApproveCategories.Contains("low"),
                AutoApproveMemoryOps = autoApproveCategories.Contains("memory"),
                AutoApproveSelfReflection = autoApproveCategories.Contains("analysis") || autoApproveCategories.Contains("reflection"),
                EnableProactiveCommunication = Config.EnablePush,
                EnableCodeModification = !autoApproveCategories.Contains("no-code"),
                Culture = Config.Culture
            };

            // Create the autonomous coordinator
            Coordinator = new AutonomousCoordinator(autonomousConfig);

            // Share coordinator with autonomous tools (enables status checks even without push mode)
            Ouroboros.Application.Tools.AutonomousTools.SharedCoordinator = Coordinator;

            // Wire up event handlers
            Coordinator.OnProactiveMessage += HandleAutonomousMessage;
            Coordinator.OnIntentionRequiresAttention += HandleIntentionAttention;

            // Configure functions if available
            if (Models.Llm != null)
            {
                Coordinator.ExecuteToolFunction = async (tool, args, ct) =>
                {
                    ITool? toolObj = Tools.Tools.All.FirstOrDefault(t => t.Name == tool);
                    if (toolObj != null)
                    {
                        Result<string, string> result = await toolObj.InvokeAsync(args, ct);
                        return result.Match(
                            success => success,
                            error => $"Tool execution failed: {error}");
                    }
                    return $"Tool '{tool}' not found.";
                };

                // Wire up ThinkFunction for autonomous topic discovery
                Coordinator.ThinkFunction = async (prompt, ct) =>
                {
                    (string response, List<ToolExecution> _) = await Models.Llm.GenerateWithToolsAsync(prompt, ct);
                    return response;
                };
            }

            if (Models.Embedding != null)
            {
                Coordinator.EmbedFunction = async (text, ct) =>
                {
                    return await Models.Embedding.CreateEmbeddingsAsync(text, ct);
                };
            }

            // Wire up Qdrant storage and search for autonomous memory
            if (Memory.NeuralMemory != null)
            {
                Coordinator.StoreToQdrantFunction = async (category, content, embedding, ct) =>
                {
                    await Memory.NeuralMemory.StoreMemoryAsync(category, content, embedding, ct);
                };

                Coordinator.SearchQdrantFunction = async (embedding, limit, ct) =>
                {
                    return await Memory.NeuralMemory.SearchMemoriesAsync(embedding, limit, ct);
                };

                // Wire up intention storage
                Coordinator.StoreIntentionFunction = async (intention, ct) =>
                {
                    await Memory.NeuralMemory.StoreIntentionAsync(intention, ct);
                };

                // Wire up neuron message storage
                Coordinator.StoreNeuronMessageFunction = async (message, ct) =>
                {
                    await Memory.NeuralMemory.StoreNeuronMessageAsync(message, ct);
                };
            }
            else if (Memory.Skills != null)
            {
                // Fallback: Use skills to find related context
                Coordinator.SearchQdrantFunction = async (embedding, limit, ct) =>
                {
                    IEnumerable<Skill> results = await Memory.Skills.FindMatchingSkillsAsync("recent topics and interests", null);
                    return results.Take(limit).Select(s => $"{s.Name}: {s.Description}").ToList();
                };
            }

            // Wire up MeTTa symbolic reasoning functions
            if (Memory.MeTTaEngine != null)
            {
                Coordinator.MeTTaQueryFunction = async (query, ct) =>
                {
                    Result<string, string> result = await Memory.MeTTaEngine.ExecuteQueryAsync(query, ct);
                    return result.Match(
                        success => success,
                        error => $"MeTTa error: {error}");
                };

                Coordinator.MeTTaAddFactFunction = async (fact, ct) =>
                {
                    Result<Unit, string> result = await Memory.MeTTaEngine.AddFactAsync(fact, ct);
                    return result.IsSuccess;
                };

                // Wire up DAG constraint verification through NetworkTracker
                if (NetworkTracker?.HasMeTTaEngine == true)
                {
                    Coordinator.VerifyDagConstraintFunction = async (branchName, constraint, ct) =>
                    {
                        Result<bool> result = await NetworkTracker.VerifyConstraintAsync(branchName, constraint, ct);
                        return result.IsSuccess && result.Value;
                    };
                }
            }

            // Wire up ProcessChatFunction for auto-training mode
            Coordinator.ProcessChatFunction = async (message, ct) =>
            {
                // Process through the main chat pipeline and return response
                string response = await ChatAsyncFunc(message);
                return response;
            };

            // Wire up FullChatWithToolsFunction for User persona in problem-solving mode
            Coordinator.FullChatWithToolsFunction = async (message, ct) =>
            {
                string response = await ChatAsyncFunc(message);
                return response;
            };

            // Wire up DisplayAndSpeakFunction for proper User→Ouroboros sequencing
            Coordinator.DisplayAndSpeakFunction = async (message, persona, ct) =>
            {
                bool isUser = persona == "User";
                Console.ForegroundColor = isUser ? ConsoleColor.Yellow : ConsoleColor.Cyan;
                Console.WriteLine($"\n  {message}");
                Console.ResetColor();

                await SayAndWaitAsyncFunc(message, persona);
            };

            // Wire up proactive message suppression for problem-solving mode
            Coordinator.SetSuppressProactiveMessages = (suppress) =>
            {
                if (AutonomousMind != null)
                {
                    AutonomousMind.SuppressProactiveMessages = suppress;
                }
            };

            // Wire up voice output (TTS) toggle
            Coordinator.SetVoiceEnabled = (enabled) =>
            {
                if (Voice.SideChannel != null)
                {
                    Voice.SideChannel.SetEnabled(enabled);
                }
            };

            // Wire up voice input (STT) toggle
            Coordinator.SetListeningEnabled = (enabled) =>
            {
                if (enabled)
                {
                    StartListeningAsyncFunc().ConfigureAwait(false);
                }
                else
                {
                    StopListeningAction();
                }
            };

            // Configure topic discovery interval
            Coordinator.TopicDiscoveryIntervalSeconds = Config.DiscoveryIntervalSeconds;

            // Populate available tools for priority resolution
            Coordinator.AvailableTools = Tools.Tools.All.Select(t => t.Name).ToHashSet();

            // Start the neural network (for status visibility) without coordination loops
            Coordinator.StartNetwork();

            Output.RecordInit("Coordinator", true, "neural network active");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Autonomous Coordinator initialization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles proactive messages from autonomous coordinator.
    /// </summary>
    internal void HandleAutonomousMessage(ProactiveMessageEventArgs args)
    {
        // Always show auto-training and user_persona messages
        bool isTrainingMessage = args.Source is "user_persona" or "auto_training";

        // Skip non-training messages during conversation loop to avoid cluttering
        if (IsInConversationLoop() && !isTrainingMessage && args.Priority < IntentionPriority.High)
            return;

        // In Normal mode, only show training and high-priority messages
        if (Config.Verbosity < OutputVerbosity.Verbose && !isTrainingMessage && args.Priority < IntentionPriority.High)
        {
            Output.WriteDebug($"[{args.Source}] {args.Message}");
        }
        else
        {
            string sourceIcon = args.Source switch
            {
                "user_persona" => "👤",
                "auto_training" => "🤖",
                _ => "🐍"
            };
            var displayMessage = args.Message.StartsWith("👤") || args.Message.StartsWith("🐍")
                ? args.Message
                : $"{sourceIcon} [{args.Source}] {args.Message}";
            Output.WriteSystem(displayMessage);
        }

        // Speak on voice side channel - block until complete
        // Use distinct persona for user_persona to get a different voice
        if (args.Priority >= IntentionPriority.Normal)
        {
            var voicePersona = args.Source == "user_persona" ? "User" : null;
            SayAndWaitAsyncFunc(args.Message, voicePersona).GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Handles intentions requiring user attention.
    /// </summary>
    internal void HandleIntentionAttention(Intention intention)
    {
        if (IsInConversationLoop()) return;

        var shortId = intention.Id.ToString()[..4];
        Output.WriteSystem($"⚡ {intention.Title} ({intention.Category}/{intention.Priority}) — /approve {shortId} | /reject {shortId}");

        // Announce intention on voice side channel
        if (intention.Priority >= IntentionPriority.Normal)
        {
            AnnounceAction($"Intention: {intention.Title}. {intention.Rationale}");
        }
    }

    /// <summary>
    /// Background loop that displays pending intentions and handles user interaction.
    /// </summary>
    internal async Task PushModeLoopAsync(CancellationToken ct)
    {
        // The PushModeLoop is now simpler since the AutonomousCoordinator handles
        // the tick loop internally. We just wait for events and keep the task alive.
        while (!ct.IsCancellationRequested && Coordinator != null)
        {
            try
            {
                // The coordinator handles its own tick loop and fires events
                // We just keep this task alive to monitor and potentially inject goals
                await Task.Delay(5000, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Output.WriteWarning($"[push] {ex.Message}");
                await Task.Delay(5000, ct);
            }
        }
    }

    /// <summary>
    /// Background loop for self-execution of queued goals.
    /// </summary>
    internal async Task SelfExecutionLoopAsync()
    {
        while (SelfExecutionEnabled && !SelfExecutionCts?.Token.IsCancellationRequested == true)
        {
            try
            {
                if (GoalQueue.TryDequeue(out var goal))
                {
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine($"\n  [self-exec] Starting autonomous goal: {goal.Description}");
                    Console.ResetColor();

                    var startTime = DateTime.UtcNow;
                    string result;
                    bool success = true;

                    try
                    {
                        // Check if this is a DSL goal (starts with pipe syntax)
                        if (goal.Description.Contains("|") || goal.Description.StartsWith("pipeline:"))
                        {
                            result = await ExecuteDslGoalAsync(goal);
                        }
                        else
                        {
                            result = await ExecuteGoalAutonomouslyAsync(goal);
                        }
                    }
                    catch (Exception ex)
                    {
                        success = false;
                        result = $"Execution failed: {ex.Message}";
                    }

                    var duration = DateTime.UtcNow - startTime;

                    // Track capability usage for self-improvement
                    await TrackGoalExecutionAsync(goal, success, duration);

                    // Reify execution into network state
                    ReifyGoalExecution(goal, result, success, duration);

                    // Update global workspace with result
                    var priority = goal.Priority switch
                    {
                        GoalPriority.Critical => WorkspacePriority.Critical,
                        GoalPriority.High => WorkspacePriority.High,
                        GoalPriority.Normal => WorkspacePriority.Normal,
                        _ => WorkspacePriority.Low
                    };
                    GlobalWorkspace?.AddItem(
                        $"Goal completed: {goal.Description}\nResult: {result}\nDuration: {duration.TotalSeconds:F2}s",
                        priority,
                        "self-execution",
                        new List<string> { "goal", success ? "completed" : "failed" });

                    // Trigger autonomous reflection on completion
                    if (success)
                    {
                        // Learn from successful execution
                        await ExecuteAutonomousActionAsync("Learn", $"Successful goal execution: {goal.Description}");
                    }
                    else
                    {
                        // Reflect on failure to improve
                        await ExecuteAutonomousActionAsync("Reflect", $"Failed goal: {goal.Description}. Result: {result}");
                    }

                    // Trigger self-evaluation periodically
                    if (GoalQueue.IsEmpty && SelfEvaluator != null)
                    {
                        await PerformPeriodicSelfEvaluationAsync();
                    }

                    Console.ForegroundColor = success ? ConsoleColor.DarkGreen : ConsoleColor.Yellow;
                    Console.WriteLine($"  [self-exec] Goal {(success ? "completed" : "failed")}: {goal.Description} ({duration.TotalSeconds:F2}s)");
                    Console.ResetColor();
                }
                else
                {
                    // Idle time - check for self-improvement opportunities and generate autonomous thoughts
                    await CheckSelfImprovementOpportunitiesAsync();

                    // Periodically run autonomous introspection cycles
                    if (Random.Shared.NextDouble() < 0.05) // 5% chance per idle cycle
                    {
                        await ExecuteAutonomousActionAsync("SelfImprove", "idle_introspection");
                    }

                    await Task.Delay(1000, SelfExecutionCts?.Token ?? CancellationToken.None);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  [self-exec] Error: {ex.Message}");
                Console.ResetColor();
            }
        }
    }

    /// <summary>
    /// Executes a DSL pipeline goal with full reification.
    /// </summary>
    internal async Task<string> ExecuteDslGoalAsync(AutonomousGoal goal)
    {
        var dsl = goal.Description.StartsWith("pipeline:")
            ? goal.Description[9..].Trim()
            : goal.Description;

        if (Models.Embedding == null || Models.Llm == null)
        {
            return "DSL execution requires LLM and embeddings to be initialized.";
        }

        var store = new TrackedVectorStore();
        var dataSource = DataSource.FromPath(".");
        var branch = new PipelineBranch($"goal-{goal.Id.ToString()[..8]}", store, dataSource);

        var state = new CliPipelineState
        {
            Branch = branch,
            Llm = Models.Llm,
            Tools = Tools.Tools,
            Embed = Models.Embedding,
            Trace = Config.Debug,
            NetworkTracker = NetworkTracker
        };

        // Track the branch for reification
        NetworkTracker?.TrackBranch(branch);

        var step = PipelineDsl.Build(dsl);
        state = await step(state);

        // Final reification update
        NetworkTracker?.UpdateBranch(state.Branch);

        // Extract output
        var lastReasoning = state.Branch.Events.OfType<PipelineReasoningStep>().LastOrDefault();
        return lastReasoning?.State.Text ?? state.Output ?? "Pipeline completed without output.";
    }

    /// <summary>
    /// Tracks goal execution for capability self-improvement.
    /// </summary>
    internal async Task TrackGoalExecutionAsync(AutonomousGoal goal, bool success, TimeSpan duration)
    {
        if (CapabilityRegistry == null) return;

        // Determine which capabilities were used
        var usedCapabilities = InferCapabilitiesFromGoal(goal.Description);

        foreach (var capName in usedCapabilities)
        {
            var result = CreateCapabilityPlanExecutionResult(success, duration, goal.Description);
            await CapabilityRegistry.UpdateCapabilityAsync(capName, result);
        }
    }

    /// <summary>
    /// Infers which capabilities were used based on goal description.
    /// </summary>
    internal List<string> InferCapabilitiesFromGoal(string description)
    {
        var caps = new List<string> { "natural_language" };
        var lower = description.ToLowerInvariant();

        if (lower.Contains("|") || lower.Contains("pipeline") || lower.Contains("dsl"))
            caps.Add("pipeline_execution");
        if (lower.Contains("plan") || lower.Contains("step") || lower.Contains("multi"))
            caps.Add("planning");
        if (lower.Contains("tool") || lower.Contains("search") || lower.Contains("fetch"))
            caps.Add("tool_use");
        if (lower.Contains("metta") || lower.Contains("query") || lower.Contains("symbol"))
            caps.Add("symbolic_reasoning");
        if (lower.Contains("remember") || lower.Contains("recall") || lower.Contains("memory"))
            caps.Add("memory_management");
        if (lower.Contains("code") || lower.Contains("program") || lower.Contains("script"))
            caps.Add("coding");

        return caps;
    }

    /// <summary>
    /// Creates an PlanExecutionResult for capability tracking purposes.
    /// This creates a minimal valid PlanExecutionResult with empty plan/steps.
    /// </summary>
    internal static PlanExecutionResult CreateCapabilityPlanExecutionResult(bool success, TimeSpan duration, string taskDescription)
    {
        var minimalPlan = new Plan(
            Goal: taskDescription,
            Steps: new List<PlanStep>(),
            ConfidenceScores: new Dictionary<string, double>(),
            CreatedAt: DateTime.UtcNow);

        return new PlanExecutionResult(
            Plan: minimalPlan,
            StepResults: new List<StepResult>(),
            Success: success,
            FinalOutput: taskDescription,
            Metadata: new Dictionary<string, object>
            {
                ["capability_tracking"] = true,
                ["timestamp"] = DateTime.UtcNow
            },
            Duration: duration);
    }

    /// <summary>
    /// Reifies goal execution into the network state (MerkleDag).
    /// </summary>
    internal void ReifyGoalExecution(AutonomousGoal goal, string result, bool success, TimeSpan duration)

    {
        if (NetworkTracker == null) return;

        // Create a synthetic branch for goal execution tracking
        var store = new TrackedVectorStore();
        var dataSource = DataSource.FromPath(".");
        var branch = new PipelineBranch($"goal-exec-{goal.Id.ToString()[..8]}", store, dataSource);

        // Add goal execution event
        branch = branch.WithIngestEvent(
            $"goal:{(success ? "success" : "failure")}",
            new[] { goal.Description, result, duration.TotalSeconds.ToString("F2") });

        NetworkTracker.TrackBranch(branch);
        NetworkTracker.UpdateBranch(branch);
    }

    /// <summary>
    /// Performs periodic self-evaluation and learning.
    /// </summary>
    internal async Task PerformPeriodicSelfEvaluationAsync()
    {
        if (SelfEvaluator == null) return;

        try
        {
            var evalResult = await SelfEvaluator.EvaluatePerformanceAsync();
            if (evalResult.IsSuccess)
            {
                var assessment = evalResult.Value;

                // Log evaluation to global workspace
                GlobalWorkspace?.AddItem(
                    $"Self-Evaluation: {assessment.OverallPerformance:P0} performance\n" +
                    $"Strengths: {string.Join(", ", assessment.Strengths.Take(3))}\n" +
                    $"Weaknesses: {string.Join(", ", assessment.Weaknesses.Take(3))}",
                    WorkspacePriority.Normal,
                    "self-evaluation",
                    new List<string> { "evaluation", "self-improvement" });

                // Check if we need to learn new capabilities
                foreach (var weakness in assessment.Weaknesses)
                {
                    await ConsiderLearningCapabilityAsync(weakness);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SelfEval] Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks for self-improvement opportunities during idle time.
    /// </summary>
    internal async Task CheckSelfImprovementOpportunitiesAsync()
    {
        if (CapabilityRegistry == null || GlobalWorkspace == null) return;

        try
        {
            // Generate autonomous thought about current state
            var thought = await GenerateAutonomousThoughtAsync();
            if (thought != null)
            {
                await ProcessAutonomousThoughtAsync(thought);
            }

            // Check for recent failures that might indicate capability gaps
            var recentItems = GlobalWorkspace.GetItems()
                .Where(i => i.Tags.Contains("failed") && i.CreatedAt > DateTime.UtcNow.AddHours(-1))
                .ToList();

            if (recentItems.Count >= 2)
            {
                // Multiple recent failures - trigger autonomous reflection
                await ExecuteAutonomousActionAsync("Reflect",
                    $"Recent failures detected: {string.Join(", ", recentItems.Select(i => i.Content[..Math.Min(50, i.Content.Length)]))}");

                // Queue learning goal using DSL
                var learningDsl = $"Set('Analyze failures: {recentItems.Count} recent') | Plan | SelfEvaluate('failure_analysis') | Learn";
                var learningGoal = new AutonomousGoal(
                    Guid.NewGuid(),
                    $"pipeline:{learningDsl}",
                    GoalPriority.Low,
                    DateTime.UtcNow);
                GoalQueue.Enqueue(learningGoal);
            }

            // Periodic autonomous introspection
            if (Random.Shared.NextDouble() < 0.1) // 10% chance each idle cycle
            {
                await ExecuteAutonomousActionAsync("SelfEvaluate", "periodic_introspection");
            }
        }
        catch
        {
            // Silent failure for background improvement checks
        }
    }

    /// <summary>
    /// Generates an autonomous thought based on current state and context.
    /// </summary>
    internal async Task<AutonomousThought?> GenerateAutonomousThoughtAsync()
    {
        if (Models.ChatModel == null || GlobalWorkspace == null) return null;

        try
        {
            // Gather context for thought generation
            var workspaceItems = GlobalWorkspace.GetItems().TakeLast(5).ToList();
            var recentContext = string.Join("\n", workspaceItems.Select(i => $"- {i.Content[..Math.Min(100, i.Content.Length)]}"));

            var capabilities = CapabilityRegistry != null
                ? await CapabilityRegistry.GetCapabilitiesAsync()
                : new List<MetaAgentCapability>();
            var capSummary = string.Join(", ", capabilities.Take(5).Select(c => $"{c.Name}({c.SuccessRate:P0})"));

            // Add language directive for thoughts if culture is specified
            string thoughtLanguageDirective = string.Empty;
            if (!string.IsNullOrEmpty(Config.Culture) && Config.Culture != "en-US")
            {
                var languageName = GetLanguageNameFunc(Config.Culture);
                thoughtLanguageDirective = $@"LANGUAGE CONSTRAINT: All thoughts MUST be generated EXCLUSIVELY in {languageName}.
Every word must be in {languageName}. Do NOT use English.

";
            }

            var thoughtPrompt = $@"{thoughtLanguageDirective}You are an autonomous AI agent with self-improvement capabilities.
Based on your current state, generate a brief autonomous thought about what you should focus on or improve.

Current capabilities: {capSummary}
Recent activity:
{recentContext}

Available autonomous actions:
- SelfEvaluate: Evaluate performance against criteria
- Learn: Synthesize learning from experience
- Plan: Create action plan for a task
- Reflect: Analyze recent actions and outcomes
- SelfImprove: Iterative improvement cycle

Generate a single autonomous thought (1-2 sentences) about what action would be most beneficial right now.
Format: [ACTION] thought content
Example: [Learn] I should consolidate my understanding of the recent coding tasks to improve future performance.";

            var response = await Models.ChatModel.GenerateTextAsync(thoughtPrompt);

            // Parse the thought
            var match = Regex.Match(response, @"\[(\w+)\]\s*(.+)", RegexOptions.Singleline);
            if (match.Success)
            {
                var actionType = match.Groups[1].Value;
                var content = match.Groups[2].Value.Trim();

                return new AutonomousThought(
                    Guid.NewGuid(),
                    actionType,
                    content,
                    DateTime.UtcNow);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AutonomousThought] Error: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Processes an autonomous thought, potentially triggering actions.
    /// </summary>
    internal async Task ProcessAutonomousThoughtAsync(AutonomousThought thought)
    {
        if (Config.Debug)
        {
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine($"  💭 [thought] [{thought.ActionType}] {thought.Content}");
            Console.ResetColor();
        }

        // Log thought to global workspace
        GlobalWorkspace?.AddItem(
            $"Autonomous thought: [{thought.ActionType}] {thought.Content}",
            WorkspacePriority.Low,
            "autonomous-thought",
            new List<string> { "thought", thought.ActionType.ToLowerInvariant() });

        // Persist thought if persistence is available
        if (Memory.ThoughtPersistence != null)
        {
            // Map action type to thought type
            var thoughtType = thought.ActionType.ToLowerInvariant() switch
            {
                "learn" => InnerThoughtType.Consolidation,
                "selfevaluate" => InnerThoughtType.Metacognitive,
                "reflect" => InnerThoughtType.SelfReflection,
                "plan" => InnerThoughtType.Strategic,
                "selfimprove" => InnerThoughtType.Intention,
                _ => InnerThoughtType.Analytical
            };

            var innerThought = InnerThought.CreateAutonomous(
                thoughtType,
                thought.Content,
                confidence: 0.7,
                priority: ThoughtPriority.Background,
                tags: new[] { "autonomous", thought.ActionType.ToLowerInvariant() });

            await Memory.ThoughtPersistence.SaveAsync(innerThought, thought.ActionType);
        }

        // Decide whether to act on the thought
        var shouldAct = thought.ActionType.ToLowerInvariant() switch
        {
            "learn" => true,
            "selfevaluate" => true,
            "reflect" => true,
            "plan" => GoalQueue.Count < 3, // Only plan if not too busy
            "selfimprove" => GoalQueue.IsEmpty, // Only improve when idle
            _ => false
        };

        if (shouldAct)
        {
            await ExecuteAutonomousActionAsync(thought.ActionType, thought.Content);
        }
    }

    /// <summary>
    /// Executes an autonomous action using the self-improvement DSL tokens.
    /// </summary>
    internal async Task ExecuteAutonomousActionAsync(string actionType, string context)
    {
        if (Models.Llm == null || Models.Embedding == null) return;

        try
        {
            // Build DSL pipeline based on action type
            var dsl = actionType.ToLowerInvariant() switch
            {
                "learn" => $"Set('{EscapeDslString(context)}') | Reify | Learn",
                "selfevaluate" => $"Set('{EscapeDslString(context)}') | Reify | SelfEvaluate('{EscapeDslString(context)}')",
                "reflect" => $"Set('{EscapeDslString(context)}') | Reify | Reflect",
                "plan" => $"Set('{EscapeDslString(context)}') | Reify | Plan('{EscapeDslString(context)}')",
                "selfimprove" => $"Set('{EscapeDslString(context)}') | Reify | SelfImprovingCycle('{EscapeDslString(context)}')",
                "autosolve" => $"Set('{EscapeDslString(context)}') | Reify | AutoSolve('{EscapeDslString(context)}')",
                _ => $"Set('{EscapeDslString(context)}') | Draft"
            };

            if (Config.Debug)
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"  [autonomous] Executing: {dsl}");
                Console.ResetColor();
            }

            // Execute the DSL pipeline
            var store = new TrackedVectorStore();
            var dataSource = DataSource.FromPath(".");
            var branch = new PipelineBranch($"autonomous-{actionType.ToLowerInvariant()}-{Guid.NewGuid().ToString()[..8]}", store, dataSource);

            var state = new CliPipelineState
            {
                Branch = branch,
                Llm = Models.Llm,
                Tools = Tools.Tools,
                Embed = Models.Embedding,
                Trace = Config.Debug,
                NetworkTracker = NetworkTracker
            };

            NetworkTracker?.TrackBranch(branch);

            var step = PipelineDsl.Build(dsl);
            state = await step(state);

            NetworkTracker?.UpdateBranch(state.Branch);

            // Extract result
            var result = state.Branch.Events.OfType<PipelineReasoningStep>().LastOrDefault()?.State.Text
                ?? state.Output
                ?? "Action completed";

            // Log result to workspace
            GlobalWorkspace?.AddItem(
                $"Autonomous action [{actionType}]: {result[..Math.Min(200, result.Length)]}",
                WorkspacePriority.Low,
                "autonomous-action",
                new List<string> { "action", actionType.ToLowerInvariant(), "autonomous" });

            if (Config.Debug)
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine($"  [autonomous] Completed: {result[..Math.Min(100, result.Length)]}...");
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AutonomousAction] Error executing {actionType}: {ex.Message}");
        }
    }

    /// <summary>
    /// Escapes a string for use in DSL arguments.
    /// </summary>
    internal static string EscapeDslString(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input
            .Replace("'", "\\'")
            .Replace("\n", " ")
            .Replace("\r", "")
            [..Math.Min(input.Length, 200)];
    }

    /// <summary>
    /// Considers learning a new capability based on identified weakness.
    /// </summary>

    internal async Task ConsiderLearningCapabilityAsync(string weakness)
    {
        if (CapabilityRegistry == null || Tools.ToolLearner == null) return;

        // Check if this is a capability we could learn
        var gaps = await CapabilityRegistry.IdentifyCapabilityGapsAsync(weakness);

        foreach (var gap in gaps)
        {
            // Queue a learning goal
            var learningGoal = new AutonomousGoal(
                Guid.NewGuid(),
                $"Learn capability: {gap} to address weakness: {weakness}",
                GoalPriority.Low,
                DateTime.UtcNow);

            GoalQueue.Enqueue(learningGoal);

            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"  [self-improvement] Queued learning goal: {gap}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Executes a goal autonomously using planning and sub-agent delegation.
    /// </summary>
    internal async Task<string> ExecuteGoalAutonomouslyAsync(AutonomousGoal goal)
    {
        var sb = new StringBuilder();

        // Step 1: Plan the goal
        if (Orchestrator != null)
        {
            var planResult = await Orchestrator.PlanAsync(goal.Description);
            if (planResult.IsSuccess)
            {
                var plan = planResult.Value;
                sb.AppendLine($"Plan created with {plan.Steps.Count} steps");

                // Step 2: Check if we should delegate to sub-agents
                if (plan.Steps.Count > 3 && DistributedOrchestrator != null)
                {
                    // Distribute to sub-agents
                    var execResult = await DistributedOrchestrator.ExecuteDistributedAsync(plan);
                    if (execResult.IsSuccess)
                    {
                        sb.AppendLine($"Distributed execution completed: {execResult.Value.FinalOutput}");
                        return sb.ToString();
                    }
                }

                // Step 3: Execute directly
                var directResult = await Orchestrator.ExecuteAsync(plan);
                if (directResult.IsSuccess)
                {
                    sb.AppendLine($"Execution completed: {directResult.Value.FinalOutput}");
                }
                else
                {
                    sb.AppendLine($"Execution failed: {directResult.Error}");
                }
            }
            else
            {
                sb.AppendLine($"Planning failed: {planResult.Error}");
            }
        }
        else
        {
            // Fall back to simple chat-based execution
            var response = await ChatAsyncFunc($"Please help me accomplish this goal: {goal.Description}");
            sb.AppendLine(response);
        }

        return sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SELF-EXECUTION COMMAND HANDLERS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Handles self-execution commands.
    /// </summary>
    internal async Task<string> SelfExecCommandAsync(string subCommand)
    {
        var cmd = subCommand.ToLowerInvariant().Trim();

        if (cmd is "" or "status")
        {
            var status = SelfExecutionEnabled ? "Active" : "Disabled";
            var queueCount = GoalQueue.Count;
            return $@"Self-Execution Status:
• Status: {status}
• Queued Goals: {queueCount}
• Completed: (tracked in global workspace)

Commands:
  selfexec start    - Enable autonomous execution
  selfexec stop     - Disable autonomous execution
  selfexec queue    - Show queued goals";
        }

        if (cmd == "start")
        {
            if (!SelfExecutionEnabled)
            {
                SelfExecutionCts?.Dispose();
                SelfExecutionCts = new CancellationTokenSource();
                SelfExecutionEnabled = true;
                SelfExecutionTask = Task.Run(SelfExecutionLoopAsync, SelfExecutionCts.Token);
            }
            return "Self-execution enabled. I will autonomously pursue queued goals.";
        }

        if (cmd == "stop")
        {
            SelfExecutionEnabled = false;
            SelfExecutionCts?.Cancel();
            return "Self-execution disabled. Goals will no longer be automatically executed.";
        }

        if (cmd == "queue")
        {
            if (GoalQueue.IsEmpty)
            {
                return "Goal queue is empty. Use 'goal add <description>' to add goals.";
            }
            var goals = GoalQueue.ToArray();
            var sb = new StringBuilder("Queued Goals:\n");
            for (int i = 0; i < goals.Length; i++)
            {
                sb.AppendLine($"  {i + 1}. [{goals[i].Priority}] {goals[i].Description}");
            }
            return sb.ToString();
        }

        return $"Unknown self-exec command: {subCommand}. Try 'selfexec status'.";
    }

    /// <summary>
    /// Handles sub-agent commands.
    /// </summary>
    internal async Task<string> SubAgentCommandAsync(string subCommand)
    {
        var cmd = subCommand.ToLowerInvariant().Trim();

        if (cmd is "" or "status" or "list")
        {
            if (DistributedOrchestrator == null)
            {
                return "Sub-agent orchestration not initialized.";
            }

            var agents = DistributedOrchestrator.GetAgentStatus();
            var sb = new StringBuilder("Registered Sub-Agents:\n");
            foreach (var agent in agents)
            {
                var statusIcon = agent.Status switch
                {
                    MetaAgentStatus.Available => "✓",
                    MetaAgentStatus.Busy => "⏳",
                    MetaAgentStatus.Offline => "✗",
                    _ => "?"
                };
                sb.AppendLine($"  {statusIcon} {agent.Name} ({agent.AgentId})");
                sb.AppendLine($"      Capabilities: {string.Join(", ", agent.Capabilities.Take(5))}");
                sb.AppendLine($"      Last heartbeat: {agent.LastHeartbeat:HH:mm:ss}");
            }
            return sb.ToString();
        }

        if (cmd.StartsWith("spawn "))
        {
            var agentName = cmd[6..].Trim();
            return await SpawnSubAgentAsync(agentName);
        }

        if (cmd.StartsWith("remove "))
        {
            var agentId = cmd[7..].Trim();
            DistributedOrchestrator?.UnregisterAgent(agentId);
            SubAgents.TryRemove(agentId, out _);
            return $"Removed sub-agent: {agentId}";
        }

        await Task.CompletedTask;
        return $"Unknown subagent command. Try: subagent list, subagent spawn <name>, subagent remove <id>";
    }

    /// <summary>
    /// Spawns a new sub-agent with specialized capabilities.
    /// </summary>
    internal async Task<string> SpawnSubAgentAsync(string agentName)
    {
        if (DistributedOrchestrator == null)
        {
            return "Sub-agent orchestration not initialized.";
        }

        var agentId = $"sub-{agentName.ToLowerInvariant()}-{Guid.NewGuid().ToString()[..8]}";

        // Determine capabilities based on name hints
        var capabilities = new HashSet<string>();
        var lowerName = agentName.ToLowerInvariant();

        if (lowerName.Contains("code") || lowerName.Contains("dev"))
            capabilities.UnionWith(new[] { "coding", "debugging", "refactoring", "testing" });
        else if (lowerName.Contains("research") || lowerName.Contains("analyst"))
            capabilities.UnionWith(new[] { "research", "analysis", "summarization", "web_search" });
        else if (lowerName.Contains("plan") || lowerName.Contains("architect"))
            capabilities.UnionWith(new[] { "planning", "architecture", "design", "decomposition" });
        else
            capabilities.UnionWith(new[] { "general", "chat", "reasoning" });

        var agent = new AgentInfo(
            agentId,
            agentName,
            capabilities,
            MetaAgentStatus.Available,
            DateTime.UtcNow);

        DistributedOrchestrator.RegisterAgent(agent);

        // Create sub-agent instance
        var subAgent = new SubAgentInstance(agentId, agentName, capabilities, Models.ChatModel);
        SubAgents[agentId] = subAgent;

        await Task.CompletedTask;
        return $"Spawned sub-agent '{agentName}' ({agentId}) with capabilities: {string.Join(", ", capabilities)}";
    }

    /// <summary>
    /// Handles epic orchestration commands.
    /// </summary>
    internal async Task<string> EpicCommandAsync(string subCommand)
    {
        var cmd = subCommand.ToLowerInvariant().Trim();

        if (cmd is "" or "status" or "list")
        {
            return "Epic Orchestration:\n• Use 'epic create <title>' to create a new epic\n• Use 'epic add <epic#> <sub-issue>' to add sub-issues";
        }

        if (cmd.StartsWith("create "))
        {
            var title = cmd[7..].Trim();
            if (EpicOrchestrator != null)
            {
                var epicNumber = new Random().Next(1000, 9999);
                var result = await EpicOrchestrator.RegisterEpicAsync(
                    epicNumber, title, "", new List<int>());

                if (result.IsSuccess)
                {
                    return $"Created epic #{epicNumber}: {title}";
                }
                return $"Failed to create epic: {result.Error}";
            }
            return "Epic orchestrator not initialized.";
        }

        await Task.CompletedTask;
        return $"Unknown epic command: {subCommand}";
    }

    /// <summary>
    /// Handles goal queue commands.
    /// </summary>
    internal async Task<string> GoalCommandAsync(string subCommand)
    {
        var cmd = subCommand.ToLowerInvariant().Trim();

        if (cmd is "" or "list")
        {
            if (GoalQueue.IsEmpty)
            {
                return "No goals in queue. Use 'goal add <description>' to add a goal.";
            }
            var goals = GoalQueue.ToArray();
            var sb = new StringBuilder("Goal Queue:\n");
            for (int i = 0; i < goals.Length; i++)
            {
                sb.AppendLine($"  {i + 1}. [{goals[i].Priority}] {goals[i].Description}");
            }
            return sb.ToString();
        }

        if (cmd.StartsWith("add "))
        {
            var description = subCommand[4..].Trim();
            var priority = description.Contains("urgent") ? GoalPriority.High
                : description.Contains("later") ? GoalPriority.Low
                : GoalPriority.Normal;

            var goal = new AutonomousGoal(Guid.NewGuid(), description, priority, DateTime.UtcNow);
            GoalQueue.Enqueue(goal);

            return $"Added goal to queue: {description} (Priority: {priority})";
        }

        if (cmd == "clear")
        {
            while (GoalQueue.TryDequeue(out _)) { }
            return "Goal queue cleared.";
        }

        await Task.CompletedTask;
        return "Goal commands: goal list, goal add <description>, goal clear";
    }

    /// <summary>
    /// Handles task delegation to sub-agents.
    /// </summary>
    internal async Task<string> DelegateCommandAsync(string taskDescription)
    {
        if (string.IsNullOrWhiteSpace(taskDescription))
        {
            return "Usage: delegate <task description>";
        }

        if (DistributedOrchestrator == null || Orchestrator == null)
        {
            return "Delegation requires sub-agent orchestration to be initialized.";
        }

        // Create a plan for the task
        var planResult = await Orchestrator.PlanAsync(taskDescription);
        if (!planResult.IsSuccess)
        {
            return $"Could not create plan for delegation: {planResult.Error}";
        }

        // Execute distributed
        var execResult = await DistributedOrchestrator.ExecuteDistributedAsync(planResult.Value);
        if (execResult.IsSuccess)
        {
            var agents = execResult.Value.Metadata.GetValueOrDefault("agents_used", 0);
            return $"Task delegated and completed using {agents} agent(s):\n{execResult.Value.FinalOutput}";
        }

        return $"Delegation failed: {execResult.Error}";
    }

    /// <summary>
    /// Handles self-model inspection commands.
    /// </summary>
    internal async Task<string> SelfModelCommandAsync(string subCommand)
    {
        var cmd = subCommand.ToLowerInvariant().Trim();

        if (cmd is "" or "status" or "identity")
        {
            if (IdentityGraph == null)
            {
                return "Self-model not initialized.";
            }

            var state = await IdentityGraph.GetStateAsync();
            var sb = new StringBuilder();
            sb.AppendLine("╔═══════════════════════════════════════╗");
            sb.AppendLine("║         SELF-MODEL IDENTITY           ║");
            sb.AppendLine("╠═══════════════════════════════════════╣");
            sb.AppendLine($"║ Agent ID: {state.AgentId.ToString()[..8],-27} ║");
            sb.AppendLine($"║ Name: {state.Name,-31} ║");
            sb.AppendLine("╠═══════════════════════════════════════╣");
            sb.AppendLine("║ Capabilities:                         ║");

            if (CapabilityRegistry != null)
            {
                var caps = await CapabilityRegistry.GetCapabilitiesAsync();
                foreach (var cap in caps.Take(5))
                {
                    sb.AppendLine($"║   • {cap.Name,-20} ({cap.SuccessRate:P0}) ║");
                }
            }

            sb.AppendLine("╚═══════════════════════════════════════╝");
            return sb.ToString();
        }

        if (cmd == "capabilities" || cmd == "caps")
        {
            if (CapabilityRegistry == null)
            {
                return "Capability registry not initialized.";
            }

            var caps = await CapabilityRegistry.GetCapabilitiesAsync();
            var sb = new StringBuilder("Agent Capabilities:\n");
            foreach (var cap in caps)
            {
                sb.AppendLine($"  • {cap.Name}");
                sb.AppendLine($"      Description: {cap.Description}");
                sb.AppendLine($"      Success Rate: {cap.SuccessRate:P0} ({cap.UsageCount} uses)");
                var toolsList = cap.RequiredTools?.Any() == true ? string.Join(", ", cap.RequiredTools) : "none";
                sb.AppendLine($"      Required Tools: {toolsList}");
            }
            return sb.ToString();
        }

        if (cmd == "workspace")
        {
            if (GlobalWorkspace == null)
            {
                return "Global workspace not initialized.";
            }

            var items = GlobalWorkspace.GetItems();
            if (!items.Any())
            {
                return "Global workspace is empty.";
            }

            var sb = new StringBuilder("Global Workspace Contents:\n");
            foreach (var item in items.Take(10))
            {
                sb.AppendLine($"  [{item.Priority}] {item.Content[..Math.Min(50, item.Content.Length)]}...");
                sb.AppendLine($"      Source: {item.Source} | Created: {item.CreatedAt:HH:mm:ss}");
            }
            return sb.ToString();
        }

        return "Self-model commands: selfmodel status, selfmodel capabilities, selfmodel workspace";
    }

    /// <summary>
    /// Handles self-evaluation commands.
    /// </summary>
    internal async Task<string> EvaluateCommandAsync(string subCommand)
    {
        var cmd = subCommand.ToLowerInvariant().Trim();

        if (SelfEvaluator == null)
        {
            return "Self-evaluator not initialized. Requires orchestrator and skill registry.";
        }

        if (cmd is "" or "performance" or "assess")
        {
            var result = await SelfEvaluator.EvaluatePerformanceAsync();
            if (result.IsSuccess)
            {
                var assessment = result.Value;
                var sb = new StringBuilder();
                sb.AppendLine("╔═══════════════════════════════════════╗");
                sb.AppendLine("║       SELF-ASSESSMENT REPORT          ║");
                sb.AppendLine("╠═══════════════════════════════════════╣");
                sb.AppendLine($"║ Overall Performance: {assessment.OverallPerformance:P0,-15} ║");
                sb.AppendLine($"║ Confidence Calibration: {assessment.ConfidenceCalibration:P0,-12} ║");
                sb.AppendLine($"║ Skill Acquisition Rate: {assessment.SkillAcquisitionRate:F2,-12} ║");
                sb.AppendLine("╠═══════════════════════════════════════╣");

                if (assessment.Strengths.Any())
                {
                    sb.AppendLine("║ Strengths:                            ║");
                    foreach (var s in assessment.Strengths.Take(3))
                    {
                        sb.AppendLine($"║   ✓ {s,-33} ║");
                    }
                }

                if (assessment.Weaknesses.Any())
                {
                    sb.AppendLine("║ Areas for Improvement:                ║");
                    foreach (var w in assessment.Weaknesses.Take(3))
                    {
                        sb.AppendLine($"║   △ {w,-33} ║");
                    }
                }

                sb.AppendLine("╚═══════════════════════════════════════╝");
                sb.AppendLine();
                sb.AppendLine("Summary:");
                sb.AppendLine(assessment.Summary);

                return sb.ToString();
            }
            return $"Evaluation failed: {result.Error}";
        }

        return "Evaluate commands: evaluate performance";
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PUSH MODE COMMANDS (migrated from OuroborosAgent)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Approves one or more pending intentions.
    /// </summary>
    internal async Task<string> ApproveIntentionAsync(string arg)
    {
        if (Coordinator == null)
        {
            return "Push mode not enabled. Use --push flag to enable.";
        }

        var sb = new StringBuilder();
        var bus = Coordinator.IntentionBus;

        if (string.IsNullOrWhiteSpace(arg) || arg.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            // Approve all pending
            var pending = bus.GetPendingIntentions().ToList();
            if (pending.Count == 0)
            {
                return "No pending intentions to approve.";
            }

            foreach (var intention in pending)
            {
                var result = bus.ApproveIntentionByPartialId(intention.Id.ToString()[..8], "User approved all");
                sb.AppendLine(result
                    ? $"✓ Approved: [{intention.Id.ToString()[..8]}] {intention.Title}"
                    : $"✗ Failed to approve: {intention.Id}");
            }
        }
        else
        {
            // Approve specific intention by ID prefix
            var result = bus.ApproveIntentionByPartialId(arg, "User approved");
            sb.AppendLine(result
                ? $"✓ Approved intention: {arg}"
                : $"No pending intention found matching '{arg}'.");
        }

        await Task.CompletedTask;
        return sb.ToString();
    }

    /// <summary>
    /// Rejects one or more pending intentions.
    /// </summary>
    internal async Task<string> RejectIntentionAsync(string arg)
    {
        if (Coordinator == null)
        {
            return "Push mode not enabled. Use --push flag to enable.";
        }

        var sb = new StringBuilder();
        var bus = Coordinator.IntentionBus;

        if (string.IsNullOrWhiteSpace(arg) || arg.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            // Reject all pending
            var pending = bus.GetPendingIntentions().ToList();
            if (pending.Count == 0)
            {
                return "No pending intentions to reject.";
            }

            foreach (var intention in pending)
            {
                bus.RejectIntentionByPartialId(intention.Id.ToString()[..8], "User rejected all");
                sb.AppendLine($"✗ Rejected: [{intention.Id.ToString()[..8]}] {intention.Title}");
            }
        }
        else
        {
            // Reject specific intention by ID prefix
            var result = bus.RejectIntentionByPartialId(arg, "User rejected");
            sb.AppendLine(result
                ? $"✗ Rejected intention: {arg}"
                : $"No pending intention found matching '{arg}'.");
        }

        await Task.CompletedTask;
        return sb.ToString();
    }

    /// <summary>
    /// Lists all pending intentions.
    /// </summary>
    internal string ListPendingIntentions()
    {
        if (Coordinator == null)
        {
            return "Push mode not enabled. Use --push flag to enable.";
        }

        var pending = Coordinator.IntentionBus.GetPendingIntentions().ToList();

        if (pending.Count == 0)
        {
            return "No pending intentions. Ouroboros will propose actions based on context.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("╔═══════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                   PENDING INTENTIONS                          ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        foreach (var intention in pending.OrderByDescending(i => i.Priority))
        {
            var priorityMarker = intention.Priority switch
            {
                IntentionPriority.Critical => "🔴",
                IntentionPriority.High => "🟠",
                IntentionPriority.Normal => "🟢",
                _ => "⚪"
            };

            sb.AppendLine($"  {priorityMarker} [{intention.Id.ToString()[..8]}] {intention.Category}");
            sb.AppendLine($"     {intention.Title}");
            sb.AppendLine($"     {intention.Description}");
            sb.AppendLine($"     Created: {intention.CreatedAt:HH:mm:ss}");
            sb.AppendLine();
        }

        sb.AppendLine("Commands: /approve <id|all> | /reject <id|all>");

        return sb.ToString();
    }

    /// <summary>
    /// Pauses push mode (stops proposing actions).
    /// </summary>
    internal string PausePushMode()
    {
        if (Coordinator == null)
        {
            return "Push mode not enabled.";
        }

        PushModeCts?.Cancel();
        return "⏸ Push mode paused. Use /resume to continue receiving proposals.";
    }

    /// <summary>
    /// Resumes push mode (continues proposing actions).
    /// </summary>
    internal string ResumePushMode()
    {
        if (Coordinator == null)
        {
            return "Push mode not enabled. Use --push flag to enable.";
        }

        if (PushModeCts == null || PushModeCts.IsCancellationRequested)
        {
            PushModeCts?.Dispose();
            PushModeCts = new CancellationTokenSource();
            PushModeTask = Task.Run(() => PushModeLoopAsync(PushModeCts.Token), PushModeCts.Token);
            return "▶ Push mode resumed. Ouroboros will propose actions.";
        }

        return "Push mode is already active.";
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CODE SELF-PERCEPTION COMMANDS (migrated from OuroborosAgent)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Direct command to save/modify code using modify_my_code tool.
    /// Bypasses LLM since some models don't properly use tools.
    /// </summary>
    internal async Task<string> SaveCodeCommandAsync(string argument)
    {
        try
        {
            // Check if we have the tool
            Maybe<ITool> toolOption = Tools.Tools.GetTool("modify_my_code");
            if (!toolOption.HasValue)
            {
                return "❌ Self-modification tool (modify_my_code) is not registered. Please restart with proper tool initialization.";
            }

            ITool tool = toolOption.GetValueOrDefault(null!)!;

            // Parse the argument - expect JSON or guided input
            if (string.IsNullOrWhiteSpace(argument))
            {
                return @"📝 **Save Code - Direct Tool Invocation**

Usage: `save {""file"":""path/to/file.cs"",""search"":""exact text to find"",""replace"":""replacement text""}`

Or use the interactive format:
  `save file.cs ""old text"" ""new text""`

Examples:
  `save {""file"":""src/Ouroboros.CLI/Commands/OuroborosAgent.cs"",""search"":""old code"",""replace"":""new code""}`
  `save MyClass.cs ""public void Old()"" ""public void New()""

This command directly invokes the `modify_my_code` tool, bypassing the LLM.";
            }

            string jsonInput;
            if (argument.TrimStart().StartsWith("{"))
            {
                // Already JSON
                jsonInput = argument;
            }
            else
            {
                // Try to parse as "file search replace" format
                // Normalize smart quotes and other quote variants to standard quotes
                string normalizedArg = argument
                    .Replace('\u201C', '"')  // Left smart quote "
                    .Replace('\u201D', '"')  // Right smart quote "
                    .Replace('\u201E', '"')  // German low quote „
                    .Replace('\u201F', '"')  // Double high-reversed-9 ‟
                    .Replace('\u2018', '\'') // Left single smart quote '
                    .Replace('\u2019', '\'') // Right single smart quote '
                    .Replace('`', '\'');     // Backtick to single quote

                // Find first quote (double or single)
                int firstDoubleQuote = normalizedArg.IndexOf('"');
                int firstSingleQuote = normalizedArg.IndexOf('\'');

                char quoteChar;
                int firstQuote;
                if (firstDoubleQuote == -1 && firstSingleQuote == -1)
                {
                    return @"❌ Invalid format. Use JSON or: filename ""search text"" ""replace text""

Example: save MyClass.cs ""old code"" ""new code""
Note: You can use double quotes ("") or single quotes ('')";
                }
                else if (firstDoubleQuote == -1)
                {
                    quoteChar = '\'';
                    firstQuote = firstSingleQuote;
                }
                else if (firstSingleQuote == -1)
                {
                    quoteChar = '"';
                    firstQuote = firstDoubleQuote;
                }
                else
                {
                    // Use whichever comes first
                    if (firstDoubleQuote < firstSingleQuote)
                    {
                        quoteChar = '"';
                        firstQuote = firstDoubleQuote;
                    }
                    else
                    {
                        quoteChar = '\'';
                        firstQuote = firstSingleQuote;
                    }
                }

                string filePart = normalizedArg[..firstQuote].Trim();
                string rest = normalizedArg[firstQuote..];

                // Parse quoted strings
                List<string> quoted = new();
                bool inQuote = false;
                StringBuilder current = new();
                for (int i = 0; i < rest.Length; i++)
                {
                    char c = rest[i];
                    if (c == quoteChar)
                    {
                        if (inQuote)
                        {
                            quoted.Add(current.ToString());
                            current.Clear();
                            inQuote = false;
                        }
                        else
                        {
                            inQuote = true;
                        }
                    }
                    else if (inQuote)
                    {
                        current.Append(c);
                    }
                }

                if (quoted.Count < 2)
                {
                    return $@"❌ Could not parse search and replace strings. Found {quoted.Count} quoted section(s).

Use format: filename ""search"" ""replace""
Or with single quotes: filename 'search' 'replace'

Make sure both search and replace text are quoted.";
                }

                jsonInput = System.Text.Json.JsonSerializer.Serialize(new
                {
                    file = filePart,
                    search = quoted[0],
                    replace = quoted[1]
                });
            }

            // Invoke the tool directly
            Console.WriteLine($"[SaveCode] Invoking modify_my_code with: {jsonInput[..Math.Min(100, jsonInput.Length)]}...");
            Result<string, string> result = await tool.InvokeAsync(jsonInput);

            if (result.IsSuccess)
            {
                return $"✅ **Code Modified Successfully**\n\n{result.Value}";
            }
            else
            {
                return $"❌ **Modification Failed**\n\n{result.Error}";
            }
        }
        catch (Exception ex)
        {
            return $"❌ SaveCode command failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Direct command to read source code using read_my_file tool.
    /// </summary>
    internal async Task<string> ReadMyCodeCommandAsync(string filePath)
    {
        try
        {
            Maybe<ITool> toolOption = Tools.Tools.GetTool("read_my_file");
            if (!toolOption.HasValue)
            {
                return "❌ Read file tool (read_my_file) is not registered.";
            }

            ITool tool = toolOption.GetValueOrDefault(null!)!;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return @"📖 **Read My Code - Direct Tool Invocation**

Usage: `read my code <filepath>`

Examples:
  `read my code src/Ouroboros.CLI/Commands/OuroborosAgent.cs`
  `/read OuroborosCommands.cs`
  `cat Program.cs`";
            }

            Console.WriteLine($"[ReadMyCode] Reading: {filePath}");
            Result<string, string> result = await tool.InvokeAsync(filePath.Trim());

            if (result.IsSuccess)
            {
                return result.Value;
            }
            else
            {
                return $"❌ Failed to read file: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            return $"❌ ReadMyCode command failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Direct command to search source code using search_my_code tool.
    /// </summary>
    internal async Task<string> SearchMyCodeCommandAsync(string query)
    {
        try
        {
            Maybe<ITool> toolOption = Tools.Tools.GetTool("search_my_code");
            if (!toolOption.HasValue)
            {
                return "❌ Search code tool (search_my_code) is not registered.";
            }

            ITool tool = toolOption.GetValueOrDefault(null!)!;

            if (string.IsNullOrWhiteSpace(query))
            {
                return @"🔍 **Search My Code - Direct Tool Invocation**

Usage: `search my code <query>`

Examples:
  `search my code tool registration`
  `/search consciousness`
  `grep modify_my_code`
  `find in code GenerateTextAsync`";
            }

            Console.WriteLine($"[SearchMyCode] Searching for: {query}");
            Result<string, string> result = await tool.InvokeAsync(query.Trim());

            if (result.IsSuccess)
            {
                return result.Value;
            }
            else
            {
                return $"❌ Search failed: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            return $"❌ SearchMyCode command failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Direct command to analyze and improve code using Roslyn tools.
    /// Bypasses LLM to use tools directly.
    /// </summary>
    internal async Task<string> AnalyzeCodeCommandAsync(string input)
    {
        StringBuilder sb = new();
        sb.AppendLine("🔍 **Code Analysis - Direct Tool Invocation**\n");

        try
        {
            // Step 1: Search for C# files to analyze
            Maybe<ITool> searchTool = Tools.Tools.GetTool("search_my_code");
            Maybe<ITool> analyzeTool = Tools.Tools.GetTool("analyze_csharp_code");
            Maybe<ITool> readTool = Tools.Tools.GetTool("read_my_file");

            if (!searchTool.HasValue)
            {
                return "❌ search_my_code tool not available.";
            }

            // Find some key C# files
            sb.AppendLine("**Scanning codebase for C# files...**\n");
            Console.WriteLine("[AnalyzeCode] Searching for key files...");

            string[] searchTerms = new[] { "OuroborosAgent", "ChatAsync", "ITool", "ToolRegistry" };
            List<string> foundFiles = new();

            foreach (string term in searchTerms)
            {
                Result<string, string> searchResult = await searchTool.GetValueOrDefault(null!)!.InvokeAsync(term);
                if (searchResult.IsSuccess)
                {
                    // Extract file paths from search results
                    foreach (string line in searchResult.Value.Split('\n'))
                    {
                        if (line.Contains(".cs") && line.Contains("src/"))
                        {
                            // Extract the file path
                            int start = line.IndexOf("src/");
                            if (start >= 0)
                            {
                                int end = line.IndexOf(".cs", start) + 3;
                                if (end > start)
                                {
                                    string filePath = line[start..end];
                                    if (!foundFiles.Contains(filePath))
                                    {
                                        foundFiles.Add(filePath);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (foundFiles.Count == 0)
            {
                foundFiles.Add("src/Ouroboros.CLI/Commands/OuroborosAgent.cs");
                foundFiles.Add("src/Ouroboros.Application/Tools/SystemAccessTools.cs");
            }

            sb.AppendLine($"Found {foundFiles.Count} files to analyze:\n");
            foreach (string file in foundFiles.Take(5))
            {
                sb.AppendLine($"  • {file}");
            }
            sb.AppendLine();

            // Step 2: If Roslyn analyzer is available, use it
            if (analyzeTool.HasValue)
            {
                sb.AppendLine("**Running Roslyn analysis...**\n");
                Console.WriteLine("[AnalyzeCode] Running Roslyn analysis...");

                string sampleFile = foundFiles.FirstOrDefault() ?? "src/Ouroboros.CLI/Commands/OuroborosAgent.cs";
                if (readTool.HasValue)
                {
                    Result<string, string> readResult = await readTool.GetValueOrDefault(null!)!.InvokeAsync(sampleFile);
                    if (readResult.IsSuccess && readResult.Value.Length < 50000)
                    {
                        // Analyze a portion of the code
                        string codeSnippet = readResult.Value.Length > 5000
                            ? readResult.Value[..5000]
                            : readResult.Value;

                        Result<string, string> analyzeResult = await analyzeTool.GetValueOrDefault(null!)!.InvokeAsync(codeSnippet);
                        if (analyzeResult.IsSuccess)
                        {
                            sb.AppendLine("**Analysis Results:**\n");
                            sb.AppendLine(analyzeResult.Value);
                        }
                    }
                }
            }

            // Step 3: Provide actionable commands
            sb.AppendLine("\n**━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━**");
            sb.AppendLine("**Direct commands to modify code:**\n");
            sb.AppendLine("```");
            sb.AppendLine($"/read {foundFiles.FirstOrDefault()}");
            sb.AppendLine($"grep <search_term>");
            sb.AppendLine($"save {{\"file\":\"{foundFiles.FirstOrDefault()}\",\"search\":\"old text\",\"replace\":\"new text\"}}");
            sb.AppendLine("```\n");
            sb.AppendLine("To make a specific change, use:");
            sb.AppendLine("  1. `/read <file>` to see current content");
            sb.AppendLine("  2. `save {\"file\":\"...\",\"search\":\"...\",\"replace\":\"...\"}` to modify");
            sb.AppendLine("**━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━**");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Code analysis failed: {ex.Message}";
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // INDEX COMMANDS (migrated from OuroborosAgent)
    // ═══════════════════════════════════════════════════════════════════════════

    internal async Task<string> ReindexFullAsync()
    {
        if (SelfIndexer == null)
            return "❌ Self-indexer not available. Qdrant may not be running.";

        try
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n  [~] Starting full workspace reindex...");
            Console.ResetColor();

            var result = await SelfIndexer.FullReindexAsync();

            var sb = new StringBuilder();
            sb.AppendLine("✅ **Full Reindex Complete**\n");
            sb.AppendLine($"  • Processed files: {result.ProcessedFiles}");
            sb.AppendLine($"  • Indexed chunks: {result.IndexedChunks}");
            sb.AppendLine($"  • Skipped files: {result.SkippedFiles}");
            sb.AppendLine($"  • Errors: {result.ErrorFiles}");
            sb.AppendLine($"  • Duration: {result.Elapsed.TotalSeconds:F1}s");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Reindex failed: {ex.Message}";
        }
    }

    internal async Task<string> ReindexIncrementalAsync()
    {
        if (SelfIndexer == null)
            return "❌ Self-indexer not available. Qdrant may not be running.";

        try
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n  [~] Starting incremental reindex (changed files only)...");
            Console.ResetColor();

            var result = await SelfIndexer.IncrementalIndexAsync();

            var sb = new StringBuilder();
            sb.AppendLine("✅ **Incremental Reindex Complete**\n");
            sb.AppendLine($"  • Updated files: {result.ProcessedFiles}");
            sb.AppendLine($"  • Indexed chunks: {result.IndexedChunks}");
            sb.AppendLine($"  • Duration: {result.Elapsed.TotalSeconds:F1}s");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Incremental reindex failed: {ex.Message}";
        }
    }

    internal async Task<string> IndexSearchAsync(string query)
    {
        if (SelfIndexer == null)
            return "❌ Self-indexer not available. Qdrant may not be running.";

        if (string.IsNullOrWhiteSpace(query))
        {
            return @"🔍 **Index Search - Semantic Code Search**

Usage: `index search <query>`

Examples:
  `index search how is TTS initialized`
  `index search error handling patterns`
  `index search tool registration`";
        }

        try
        {
            var results = await SelfIndexer.SearchAsync(query, limit: 5);

            var sb = new StringBuilder();
            sb.AppendLine($"🔍 **Index Search Results for:** \"{query}\"\n");

            if (results.Count == 0)
            {
                sb.AppendLine("No results found. Try running `reindex` to update the index.");
            }
            else
            {
                foreach (var result in results)
                {
                    sb.AppendLine($"**{result.FilePath}** (score: {result.Score:F2})");
                    sb.AppendLine($"```");
                    sb.AppendLine(result.Content.Length > 500 ? result.Content[..500] + "..." : result.Content);
                    sb.AppendLine($"```\n");
                }
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Index search failed: {ex.Message}";
        }
    }

    internal async Task<string> GetIndexStatsAsync()
    {
        if (SelfIndexer == null)
            return "❌ Self-indexer not available. Qdrant may not be running.";

        try
        {
            var stats = await SelfIndexer.GetStatsAsync();

            var sb = new StringBuilder();
            sb.AppendLine("📊 **Code Index Statistics**\n");
            sb.AppendLine($"  • Collection: {stats.CollectionName}");
            sb.AppendLine($"  • Total vectors: {stats.TotalVectors}");
            sb.AppendLine($"  • Indexed files: {stats.IndexedFiles}");
            sb.AppendLine($"  • Vector size: {stats.VectorSize}");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Failed to get index stats: {ex.Message}";
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // END MIGRATED METHODS
    // ═══════════════════════════════════════════════════════════════════════════

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

        // Dispose network projector
        if (NetworkProjector != null)
            await NetworkProjector.DisposeAsync();

        IsInitialized = false;
    }
}
