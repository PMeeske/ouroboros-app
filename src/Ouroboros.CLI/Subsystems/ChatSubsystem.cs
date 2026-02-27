// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Services;
using Ouroboros.Application.Streams;
using Ouroboros.Application.Tools;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Resources;
using Ouroboros.Domain;
using Spectre.Console;
using PipelineGoal = Ouroboros.Pipeline.Planning.Goal;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

/// <summary>
/// Chat subsystem: owns the full LLM chat pipeline — prompt construction,
/// smart tool selection, post-processing, thought persistence, and learning.
/// </summary>
public sealed partial class ChatSubsystem : IChatSubsystem
{
    public string Name => "Chat";
    public bool IsInitialized { get; private set; }

    private OuroborosConfig _config = null!;
    private IConsoleOutput _output = null!;
    private VoiceModeService _voiceService = null!;
    private ModelSubsystem _modelsSub = null!;
    private ToolSubsystem _toolsSub = null!;
    private MemorySubsystem _memorySub = null!;
    private EmbodimentSubsystem _embodimentSub = null!;
    private CognitiveSubsystem _cognitiveSub = null!;
    private AutonomySubsystem _autonomySub = null!;

    // Tracking state (previously agent-local fields)
    private string? _lastUserInput;
    private DateTime _lastInteractionStart;

    // High-priority integrated systems (resolved from DI via RegisterEngineInterfaces)
    private Ouroboros.Pipeline.Memory.IEpisodicMemoryEngine? _episodicMemory;
    private readonly Ouroboros.Pipeline.Metacognition.MetacognitiveReasoner _metacognition = new();
    private Ouroboros.Agent.NeuralSymbolic.INeuralSymbolicBridge? _neuralSymbolicBridge;
    private Ouroboros.Core.Reasoning.ICausalReasoningEngine _causalReasoning = new Ouroboros.Core.Reasoning.CausalReasoningEngine();
    private Ouroboros.Agent.MetaAI.ICuriosityEngine? _curiosityEngine;
    private int _responseCount;
    private Ouroboros.CLI.Sovereignty.PersonaSovereigntyGate? _sovereigntyGate;

    // Delegates wired by agent during WireCrossSubsystemDependencies
    internal Func<InnerThought, string?, Task> PersistThoughtFunc { get; set; } =
        (_, _) => Task.CompletedTask;
    internal Func<Guid, string, string, bool, double, Task> PersistThoughtResultFunc { get; set; } =
        (_, _, _, _, _) => Task.CompletedTask;
    internal Func<string, string> GetLanguageNameFunc { get; set; } =
        culture => culture;
    internal CognitiveStreamEngine? CognitiveStreamEngine { get; set; }

    public Task InitializeAsync(SubsystemInitContext ctx)
    {
        _config = ctx.Config;
        _output = ctx.Output;
        _voiceService = ctx.Voice.Service;
        _modelsSub = ctx.Models;
        _toolsSub = ctx.Tools;
        _memorySub = ctx.Memory;
        _embodimentSub = ctx.Embodiment;
        _cognitiveSub = ctx.Cognitive;
        _autonomySub = ctx.Autonomy;
        IsInitialized = true;

        // ── Engine interfaces (resolved from DI — registered in RegisterEngineInterfaces) ──
        _episodicMemory = ctx.Services?.GetService<Ouroboros.Pipeline.Memory.IEpisodicMemoryEngine>();
        _causalReasoning = ctx.Services?.GetService<Ouroboros.Core.Reasoning.ICausalReasoningEngine>()
            ?? new Ouroboros.Core.Reasoning.CausalReasoningEngine();

        // ── Bridge EpisodicMemoryTool ↔ Qdrant EpisodicMemoryEngine ──────────
        // The tool uses a session-local list; these delegates route store/recall through the
        // Qdrant-backed engine so memories persist across sessions.
        if (_episodicMemory != null)
        {
            var toolCtx = Ouroboros.Application.Tools.AutonomousTools.DefaultContext;
            toolCtx.EpisodicExternalStoreFunc =
                async (content, emotion, sig, ct) =>
                {
                    try
                    {
                        var store      = new Ouroboros.Domain.Vectors.TrackedVectorStore();
                        var dataSource = LangChain.DocumentLoaders.DataSource.FromPath(System.Environment.CurrentDirectory);
                        var branch     = new Ouroboros.Pipeline.Branches.PipelineBranch("episodic_tool", store, dataSource);
                        var execCtx    = Ouroboros.Pipeline.Memory.ExecutionContext.WithGoal(content[..Math.Min(80, content.Length)]);
                        var outcome    = Ouroboros.Pipeline.Memory.Outcome.Successful("episodic_memory tool", TimeSpan.Zero);
                        var metadata   = System.Collections.Immutable.ImmutableDictionary<string, object>.Empty
                            .Add("summary",      content[..Math.Min(200, content.Length)])
                            .Add("emotion",      emotion)
                            .Add("significance", sig.ToString("F2"));
                        await _episodicMemory.StoreEpisodeAsync(branch, execCtx, outcome, metadata, ct).ConfigureAwait(false);
                    }
                    catch (Exception) { /* non-fatal */ }
                };

            toolCtx.EpisodicExternalRecallFunc =
                async (query, count, ct) =>
                {
                    var r = await _episodicMemory.RetrieveSimilarEpisodesAsync(
                        query, topK: count, minSimilarity: 0.5, ct).ConfigureAwait(false);
                    if (!r.IsSuccess) return [];
                    return r.Value
                        .Select(e => e.Context.GetValueOrDefault("summary")?.ToString() ?? "")
                        .Where(s => !string.IsNullOrEmpty(s));
                };
        }

        // ── Neural-symbolic bridge ────────────────────────────────────────────
        if (ctx.Models.ChatModel != null && ctx.Memory.MeTTaEngine != null)
        {
            try
            {
                var kb = new Ouroboros.Agent.NeuralSymbolic.SymbolicKnowledgeBase(ctx.Memory.MeTTaEngine);
                _neuralSymbolicBridge = new Ouroboros.Agent.NeuralSymbolic.NeuralSymbolicBridge(ctx.Models.ChatModel, kb);
            }
            catch (Exception) { }
        }

        // ── Curiosity engine ──────────────────────────────────────────────────
        if (ctx.Models.ChatModel != null && ctx.Memory.Skills != null)
        {
            try
            {
                var ethics = Ouroboros.Core.Ethics.EthicsFrameworkFactory.CreateDefault();
                var memStore = new Ouroboros.Agent.MetaAI.MemoryStore(ctx.Models.Embedding);
                var safetyGuard = new Ouroboros.Agent.MetaAI.SafetyGuard(
                    Ouroboros.Agent.MetaAI.PermissionLevel.Read, ctx.Memory.MeTTaEngine);
                _curiosityEngine = new Ouroboros.Agent.MetaAI.CuriosityEngine(
                    ctx.Models.ChatModel, memStore, ctx.Memory.Skills, safetyGuard, ethics);

                // Iaret's sovereignty gate — master control
                if (ctx.Models.ChatModel != null)
                {
                    try { _sovereigntyGate = new Ouroboros.CLI.Sovereignty.PersonaSovereigntyGate(ctx.Models.ChatModel); }
                    catch (Exception) { }
                }

                var mind = ctx.Autonomy.AutonomousMind;
                if (mind != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            while (true)
                            {
                                await Task.Delay(TimeSpan.FromSeconds(90)).ConfigureAwait(false);
                                if (await _curiosityEngine.ShouldExploreAsync().ConfigureAwait(false))
                                {
                                    var opps = await _curiosityEngine
                                        .IdentifyExplorationOpportunitiesAsync(2).ConfigureAwait(false);
                                    foreach (var opp in opps)
                                    {
                                        if (_sovereigntyGate != null)
                                        {
                                            var v = await _sovereigntyGate
                                                .EvaluateExplorationAsync(opp.Description)
                                                .ConfigureAwait(false);
                                            if (!v.Approved) continue;
                                        }
                                        mind.InjectTopic(opp.Description);
                                    }
                                }
                            }
                        }
                        catch (Exception) { }
                    });
                }
            }
            catch (Exception) { }
        }

        ctx.Output.RecordInit("Chat", true, "pipeline ready");
        return Task.CompletedTask;
    }

    public async Task<string> ChatAsync(string input)
    {
        var activeLlm = _modelsSub.Llm;
        if (activeLlm == null)
        {
            var effectiveModel = (IChatCompletionModel?)_modelsSub.OrchestratedModel ?? _modelsSub.ChatModel;
            if (effectiveModel != null)
            {
                activeLlm = new ToolAwareChatModel(effectiveModel, _toolsSub.Tools);
                _modelsSub.Llm = activeLlm;
            }
        }

        if (activeLlm == null)
            return "I need an LLM connection to chat. Check if Ollama is running.";

        _lastUserInput = input;
        _lastInteractionStart = DateTime.UtcNow;

        // Build full prompt from all context sources
        string prompt = await BuildChatPromptAsync(input);

        try
        {
            await TryDetectPersonAsync(input);

            string response;
            List<ToolExecution> tools;
            using (var spinner = _output.StartSpinner("Thinking..."))
            {
                (response, tools) = await activeLlm.GenerateWithToolsAsync(prompt);
            }

            return await ProcessLlmResponseAsync(input, response, tools);
        }
        catch (Exception ex)
        {
            return $"I had trouble processing that: {ex.Message}";
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
