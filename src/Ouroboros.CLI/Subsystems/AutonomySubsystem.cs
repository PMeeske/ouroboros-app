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
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Subsystems.Autonomy;
using Ouroboros.Core.Configuration;
using Ouroboros.Domain.Voice;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Network;
using Spectre.Console;
using Ouroboros.Tools.MeTTa;
using Qdrant.Client;
using static Ouroboros.Application.Tools.AutonomousTools;
using MetaAgentCapability = Ouroboros.Agent.MetaAI.AgentCapability;
using MetaAgentStatus = Ouroboros.Agent.MetaAI.AgentStatus;

/// <summary>
/// Autonomy subsystem implementation owning all autonomous behavior and self-management.
/// </summary>
public sealed partial class AutonomySubsystem : IAutonomySubsystem
{
    public string Name => "Autonomy";
    public bool IsInitialized { get; private set; }

    // Autonomous Mind
    public AutonomousMind? AutonomousMind { get; set; }
    public AutonomousActionEngine? ActionEngine { get; set; }
    public AutonomousCoordinator? Coordinator { get; set; }
    public MetaAIPlannerOrchestrator? Orchestrator { get; set; }

    // Self-Execution
    public ConcurrentQueue<AutonomousGoal> GoalQueue { get; } = new();
    public Task? SelfExecutionTask { get; set; }
    public CancellationTokenSource? SelfExecutionCts { get; set; }
    public bool SelfExecutionEnabled { get; set; }

    // ── Extracted managers ──
    private readonly SubAgentOrchestrationManager _subAgentManager = new();
    private readonly SelfModelManager _selfModelManager = new();
    private readonly NetworkStateManager _networkStateManager = new();
    private SaveCodeCommandHandler? _saveCodeHandler;

    // Sub-Agent Orchestration (delegate to manager)
    public ConcurrentDictionary<string, SubAgentInstance> SubAgents => _subAgentManager.SubAgents;
    public IDistributedOrchestrator? DistributedOrchestrator
    {
        get => _subAgentManager.DistributedOrchestrator;
        set { /* retained for interface/setter compat; manager owns actual init */ }
    }
    public IEpicBranchOrchestrator? EpicOrchestrator
    {
        get => _subAgentManager.EpicOrchestrator;
        set { /* retained for interface/setter compat; manager owns actual init */ }
    }

    // Self-Model (delegate to manager)
    public IIdentityGraph? IdentityGraph
    {
        get => _selfModelManager.IdentityGraph;
        set { /* retained for interface/setter compat; manager owns actual init */ }
    }
    public IGlobalWorkspace? GlobalWorkspace
    {
        get => _selfModelManager.GlobalWorkspace;
        set { /* retained for interface/setter compat; manager owns actual init */ }
    }
    public IPredictiveMonitor? PredictiveMonitor
    {
        get => _selfModelManager.PredictiveMonitor;
        set { /* retained for interface/setter compat; manager owns actual init */ }
    }
    public ISelfEvaluator? SelfEvaluator
    {
        get => _selfModelManager.SelfEvaluator;
        set { /* retained for interface/setter compat; manager owns actual init */ }
    }
    public ICapabilityRegistry? CapabilityRegistry
    {
        get => _selfModelManager.CapabilityRegistry;
        set { /* retained for interface/setter compat; manager owns actual init */ }
    }

    // Self-Assembly
    public SelfAssemblyEngine? SelfAssemblyEngine { get; set; }
    public BlueprintAnalyzer? BlueprintAnalyzer { get; set; }
    public MeTTaBlueprintValidator? BlueprintValidator { get; set; }

    // Self-Code Perception
    public QdrantSelfIndexer? SelfIndexer { get; set; }

    // Network State (delegate to manager)
    public NetworkStateTracker? NetworkTracker
    {
        get => _networkStateManager.NetworkTracker;
        set { /* retained for interface/setter compat; manager owns actual init */ }
    }

    // Persistent Network State Projector (delegate to manager)
    public PersistentNetworkStateProjector? NetworkProjector
    {
        get => _networkStateManager.NetworkProjector;
        set { /* retained for interface/setter compat; manager owns actual init */ }
    }

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
    internal Func<Task> StopListeningAsyncAction { get; set; } = () => Task.CompletedTask;

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

        // ── Autonomous Action Engine (on by default; fires every 3 min) ──
        ActionEngine = new AutonomousActionEngine(TimeSpan.FromMinutes(3));
        ctx.Output.RecordInit("Autonomous Action Engine", true, "3 min interval (delegates pending)");

        // ── Sub-Agent Orchestration ──
        await _subAgentManager.InitializeCoreAsync(ctx);

        // ── Self-Model (identity, capabilities, global workspace) ──
        await _selfModelManager.InitializeCoreAsync(ctx, Orchestrator);

        // ── Network State (Merkle-DAG + Qdrant) ──
        await _networkStateManager.InitializeNetworkStateCoreAsync(ctx);

        // ── Persistent Network State Projector (learning persistence) ──
        await _networkStateManager.InitializeNetworkProjectorCoreAsync(ctx);

        // ── SaveCode command handler ──
        _saveCodeHandler = new SaveCodeCommandHandler(name => Tools.Tools.GetTool(name));

        // ── Self-Indexer (code perception) ──
        await InitializeSelfIndexerCoreAsync(ctx);

        // ── Self-Assembly ──
        await InitializeSelfAssemblyCoreAsync(ctx);

        // ── Self-Execution (background goal pursuit) ──
        // NOTE: SelfExecution loop references agent methods → wired by mediator

        MarkInitialized();
    }

    // NOTE: InitializeSubAgentOrchestrationCoreAsync, InitializeSelfModelCoreAsync,
    // InitializeNetworkStateCoreAsync, InitializeNetworkProjectorCoreAsync, and NormalizeEndpoint
    // have been extracted to SubAgentOrchestrationManager, SelfModelManager, and NetworkStateManager
    // in the Autonomy/ subdirectory.

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
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn($"⚠ SelfIndex: {ex.Message}")}");
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
                    catch (Exception) { return "False"; }
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
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn($"⚠ SelfAssembly: {ex.Message}")}");
            SelfAssemblyEngine = null;
        }

        await Task.CompletedTask;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DISPOSE
    // ═══════════════════════════════════════════════════════════════════════════

    public async ValueTask DisposeAsync()
    {
        // Stop self-execution
        SelfExecutionEnabled = false;
        SelfExecutionCts?.Cancel();
        if (SelfExecutionTask != null)
        {
            try { await SelfExecutionTask; } catch (OperationCanceledException) { /* expected on shutdown */ }
        }
        SelfExecutionCts?.Dispose();

        // Stop push mode
        PushModeCts?.Cancel();
        if (PushModeTask != null)
        {
            try { await PushModeTask; } catch (OperationCanceledException) { /* expected on shutdown */ }
        }
        PushModeCts?.Dispose();

        // Clear sub-agents
        _subAgentManager.Clear();

        // Dispose autonomous mind and action engine
        AutonomousMind?.Dispose();
        ActionEngine?.Dispose();

        // Dispose self-indexer (stops file watchers)
        if (SelfIndexer != null)
            await SelfIndexer.DisposeAsync();

        // Dispose self-assembly engine (stops assembled neurons)
        if (SelfAssemblyEngine != null)
            await SelfAssemblyEngine.DisposeAsync();

        // Dispose network state manager (tracker + projector)
        await _networkStateManager.DisposeAsync();

        IsInitialized = false;
    }
}
