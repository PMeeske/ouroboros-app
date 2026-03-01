// <copyright file="ImmersiveMode.Skills.cs" company="Ouroboros">
// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.
// </copyright>

namespace Ouroboros.CLI.Commands;

using System.Text;
using System.Text.RegularExpressions;
using LangChain.Providers.Ollama;
using Ouroboros.Abstractions.Agent;
using Ouroboros.Agent;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Application;
using Ouroboros.Application.Services;
using Ouroboros.Application.Tools;
using Ouroboros.Options;
using Ouroboros.Abstractions.Monads;
using Qdrant.Client;
using Spectre.Console;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Abstractions;
using Ouroboros.Core.Configuration;
using LangChain.DocumentLoaders;

/// <summary>
/// Skills initialization and in-memory skill registry.
/// Skill action handlers are in <see cref="ImmersiveMode"/> (ImmersiveMode.SkillHandlers.cs).
/// Tool action handlers are in <see cref="ImmersiveMode"/> (ImmersiveMode.ToolHandlers.cs).
/// </summary>
public sealed partial class ImmersiveMode
{
    /// <summary>
    /// Initialize skills, tools, and pipeline tokens.
    /// </summary>
    private async Task InitializeSkillsAsync(
        IVoiceOptions options,
        IEmbeddingModel? embeddingModel,
        IMeTTaEngine mettaEngine)
    {
        // Skip heavy initialization if subsystems already provide these
        if (HasSubsystems)
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok("[OK] Skills and tools provided by agent subsystems")}");
            // Still need pipeline tokens and state for DSL commands
            _allTokens = SkillCliSteps.GetAllPipelineTokens();
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] Discovered {_allTokens.Count} pipeline tokens")}");
            return;
        }

        try
        {
            // Initialize skill registry with Qdrant persistence if available
            if (embeddingModel != null)
            {
                // Detect vector size from embedding model
                var testEmbed = await embeddingModel.CreateEmbeddingsAsync("test");
                var vectorSize = testEmbed.Length > 0 ? testEmbed.Length : 32;

                QdrantSkillRegistry qdrantRegistry;
                var skClient = _serviceProvider?.GetService<QdrantClient>();
                var skRegistry = _serviceProvider?.GetService<IQdrantCollectionRegistry>();
                var skSettings = _serviceProvider?.GetService<QdrantSettings>();
                if (skClient != null && skRegistry != null && skSettings != null)
                {
                    qdrantRegistry = new QdrantSkillRegistry(skClient, skRegistry, skSettings, embeddingModel);
                }
                else
                {
                    var config = new QdrantSkillConfig(options.QdrantEndpoint, "ouroboros_skills", true, vectorSize);
                    qdrantRegistry = new QdrantSkillRegistry(embeddingModel, config);
                }
                await qdrantRegistry.InitializeAsync();
                _tools.SkillRegistry = qdrantRegistry;
                var allSkillsResult = await _tools.SkillRegistry.GetAllSkillsAsync();
                var skillCount = allSkillsResult.IsSuccess ? allSkillsResult.Value.Count : 0;
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] Loaded {skillCount} skills from Qdrant")}");
            }
            else
            {
                // Use a simple in-memory implementation
                _tools.SkillRegistry = new SimpleInMemorySkillRegistry();
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim("[~] Using in-memory skill storage (no embeddings)")}");
            }

            // Initialize pipeline tokens
            _allTokens = SkillCliSteps.GetAllPipelineTokens();
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] Discovered {_allTokens.Count} pipeline tokens")}");

            // Initialize dynamic tool factory
            var provider = new OllamaProvider(options.Endpoint);
            var chatModel = new OllamaChatModel(provider, options.Model);
            var toolsRegistry = new ToolRegistry();
            var toolAwareLlm = new ToolAwareChatModel(new OllamaChatAdapter(chatModel), toolsRegistry);
            _tools.DynamicToolFactory = new DynamicToolFactory(toolAwareLlm);

            // Register built-in tools including Google Search
            _tools.DynamicTools = _tools.DynamicTools
                .WithTool(_tools.DynamicToolFactory.CreateWebSearchTool("duckduckgo"))
                .WithTool(_tools.DynamicToolFactory.CreateUrlFetchTool())
                .WithTool(_tools.DynamicToolFactory.CreateCalculatorTool())
                .WithTool(_tools.DynamicToolFactory.CreateGoogleSearchTool());

            AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim($"[DEBUG] After factory tools: {_tools.DynamicTools.Count} tools")}");

            // Register comprehensive system access tools for PC control
            var systemTools = SystemAccessTools.CreateAllTools().ToList();
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim($"[DEBUG] SystemAccessTools.CreateAllTools returned {systemTools.Count} tools")}");
            foreach (var tool in systemTools)
            {
                _tools.DynamicTools = _tools.DynamicTools.WithTool(tool);
            }
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim($"[DEBUG] After system tools: {_tools.DynamicTools.Count} tools")}");

            // Register perception tools for proactive screen/camera monitoring
            var perceptionTools = PerceptionTools.CreateAllTools().ToList();
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim($"[DEBUG] PerceptionTools returned {perceptionTools.Count} tools")}");
            foreach (var tool in perceptionTools)
            {
                _tools.DynamicTools = _tools.DynamicTools.WithTool(tool);
            }
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim($"[DEBUG] After perception tools: {_tools.DynamicTools.Count} tools")}");

            // OpenClaw Gateway integration (CLI options -> env vars -> defaults)
            var openClawOpts = options as ImmersiveCommandVoiceOptions;
            bool enableOpenClaw = openClawOpts?.EnableOpenClaw ?? Environment.GetEnvironmentVariable("OPENCLAW_DISABLE") == null;
            if (enableOpenClaw)
            {
                try
                {
                    var gw = await OpenClawTools.ConnectGatewayAsync(
                        openClawOpts?.OpenClawGateway,
                        openClawOpts?.OpenClawToken);
                    _tools.DynamicTools = _tools.DynamicTools.WithOpenClawTools();
                    AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] OpenClaw gateway {gw} (5 tools)")}");
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine(OuroborosTheme.Warn(
                        $"  [!] OpenClaw: {Markup.Escape(ex.Message)}"));
                }
            }

            AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim($"[DEBUG] Final tool count: {_tools.DynamicTools.Count} tools")}");

            // Initialize vision service for AI-powered visual understanding
            var visionService = new VisionService(new VisionConfig
            {
                OllamaEndpoint = options.Endpoint,
                OllamaVisionModel = "qwen3-vl:235b-cloud", // Strong vision model from swarm
            });
            PerceptionTools.VisionService = visionService;
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok("[OK] Vision service initialized (AI-powered visual understanding)")}");

            // Subscribe to perception events for proactive responses
            PerceptionTools.OnScreenChanged += async (msg) =>
            {
                AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Accent($"[Screen Change Detected] {msg}")}");
                await Task.CompletedTask;
            };
            PerceptionTools.OnUserActivity += async (msg) =>
            {
                AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Dim($"[User Activity] {msg}")}");
                await Task.CompletedTask;
            };

            AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] Dynamic Tool Factory ready (4 built-in + {systemTools.Count} system + {perceptionTools.Count} perception tools)")}");

            // Initialize pipeline execution state
            var vectorStore = new TrackedVectorStore();
            _pipelineState = new CliPipelineState
            {
                Branch = new PipelineBranch("immersive-pipeline", vectorStore, DataSource.FromPath(Environment.CurrentDirectory)),
                Llm = toolAwareLlm,
                Tools = toolsRegistry,
                Embed = embeddingModel ?? new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text")),
                Topic = "",
                Query = "",
                Prompt = "",
                VectorStore = vectorStore,
                MeTTaEngine = mettaEngine,
            };
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok("[OK] Pipeline execution engine ready")}");

            // Initialize intelligent tool learner
            if (embeddingModel != null)
            {
                var tlClient = _serviceProvider?.GetService<QdrantClient>();
                var tlRegistry = _serviceProvider?.GetService<IQdrantCollectionRegistry>();
                if (tlClient != null && tlRegistry != null)
                {
                    _tools.ToolLearner = new IntelligentToolLearner(
                        _tools.DynamicToolFactory,
                        mettaEngine,
                        embeddingModel,
                        toolAwareLlm,
                        tlClient,
                        tlRegistry);
                }
                else
                {
                    _tools.ToolLearner = new IntelligentToolLearner(
                        _tools.DynamicToolFactory,
                        mettaEngine,
                        embeddingModel,
                        toolAwareLlm,
                        options.QdrantEndpoint);
                }
                await _tools.ToolLearner.InitializeAsync();
                var stats = _tools.ToolLearner.GetStats();
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] Intelligent Tool Learner ready ({stats.TotalPatterns} patterns)")}");

                // Initialize interconnected learner for tool-skill bridging
                _tools.InterconnectedLearner = new InterconnectedLearner(
                    _tools.DynamicToolFactory,
                    _tools.SkillRegistry!,
                    mettaEngine,
                    embeddingModel,
                    toolAwareLlm);
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok("[OK] Interconnected skill-tool learning ready")}");

                // Initialize Qdrant self-indexer for workspace content
                var siClient = _serviceProvider?.GetService<QdrantClient>();
                var siRegistry = _serviceProvider?.GetService<IQdrantCollectionRegistry>();
                if (siClient != null && siRegistry != null)
                {
                    var indexerConfig = new QdrantIndexerConfig
                    {
                        RootPaths = new List<string> { Environment.CurrentDirectory },
                        EnableFileWatcher = true
                    };
                    _tools.SelfIndexer = new QdrantSelfIndexer(siClient, siRegistry, embeddingModel, indexerConfig);
                }
                else
                {
                    var indexerConfig = new QdrantIndexerConfig
                    {
                        QdrantEndpoint = options.QdrantEndpoint,
                        RootPaths = new List<string> { Environment.CurrentDirectory },
                        EnableFileWatcher = true
                    };
                    _tools.SelfIndexer = new QdrantSelfIndexer(embeddingModel, indexerConfig);
                }
                _tools.SelfIndexer.OnFileIndexed += (file, chunks) =>
                    AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim($"[Index] {Path.GetFileName(file)} ({chunks} chunks)")}");
                await _tools.SelfIndexer.InitializeAsync();

                // Wire up the shared indexer for system access tools
                SystemAccessTools.SharedIndexer = _tools.SelfIndexer;

                var indexStats = await _tools.SelfIndexer.GetStatsAsync();
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] Self-indexer ready ({indexStats.IndexedFiles} files, {indexStats.TotalVectors} vectors)")}");
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} [!] Skills initialization error: {Markup.Escape(ex.Message)}[/]");
            _tools.SkillRegistry = new SimpleInMemorySkillRegistry();
        }
    }

    /// <summary>
    /// Simple in-memory skill registry when Qdrant is not available.
    /// </summary>
    private sealed class SimpleInMemorySkillRegistry : ISkillRegistry
    {
        private readonly List<AgentSkill> _skills = [];

        public Task<Result<Unit, string>> RegisterSkillAsync(AgentSkill skill, CancellationToken ct = default)
        {
            _skills.Add(skill);
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }

        public Result<Unit, string> RegisterSkill(AgentSkill skill)
        {
            _skills.Add(skill);
            return Result<Unit, string>.Success(Unit.Value);
        }

        public Task<Result<AgentSkill, string>> GetSkillAsync(string skillId, CancellationToken ct = default)
        {
            var skill = _skills.FirstOrDefault(s => s.Id.Equals(skillId, StringComparison.OrdinalIgnoreCase)
                || s.Name.Equals(skillId, StringComparison.OrdinalIgnoreCase));
            return skill is not null
                ? Task.FromResult(Result<AgentSkill, string>.Success(skill))
                : Task.FromResult(Result<AgentSkill, string>.Failure($"Skill '{skillId}' not found"));
        }

        public AgentSkill? GetSkill(string skillId) =>
            _skills.FirstOrDefault(s => s.Id.Equals(skillId, StringComparison.OrdinalIgnoreCase)
                || s.Name.Equals(skillId, StringComparison.OrdinalIgnoreCase));

        public Task<Result<IReadOnlyList<AgentSkill>, string>> FindSkillsAsync(
            string? category = null, IReadOnlyList<string>? tags = null, CancellationToken ct = default)
        {
            IEnumerable<AgentSkill> results = _skills;
            if (category is not null)
                results = results.Where(s => s.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
            if (tags is { Count: > 0 })
                results = results.Where(s => tags.Any(t => s.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)));
            return Task.FromResult(Result<IReadOnlyList<AgentSkill>, string>.Success(results.ToList().AsReadOnly()));
        }

        public Task<List<Skill>> FindMatchingSkillsAsync(
            string goal, Dictionary<string, object>? context = null, CancellationToken ct = default) =>
            Task.FromResult(_skills
                .Where(s => s.Name.Contains(goal, StringComparison.OrdinalIgnoreCase)
                    || s.Description.Contains(goal, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.ToSkill())
                .ToList());

        public Task<Result<Unit, string>> UpdateSkillAsync(AgentSkill skill, CancellationToken ct = default)
        {
            var idx = _skills.FindIndex(s => s.Id == skill.Id);
            if (idx < 0)
                return Task.FromResult(Result<Unit, string>.Failure($"Skill '{skill.Id}' not found"));
            _skills[idx] = skill;
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }

        public Task<Result<Unit, string>> RecordExecutionAsync(
            string skillId, bool success, long executionTimeMs, CancellationToken ct = default)
        {
            // No-op for simple registry
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }

        public void RecordSkillExecution(string skillId, bool success, long executionTimeMs)
        {
            // No-op for simple registry
        }

        public Task<Result<Unit, string>> UnregisterSkillAsync(string skillId, CancellationToken ct = default)
        {
            var removed = _skills.RemoveAll(s => s.Id.Equals(skillId, StringComparison.OrdinalIgnoreCase));
            return removed > 0
                ? Task.FromResult(Result<Unit, string>.Success(Unit.Value))
                : Task.FromResult(Result<Unit, string>.Failure($"Skill '{skillId}' not found"));
        }

        public Task<Result<IReadOnlyList<AgentSkill>, string>> GetAllSkillsAsync(CancellationToken ct = default) =>
            Task.FromResult(Result<IReadOnlyList<AgentSkill>, string>.Success((IReadOnlyList<AgentSkill>)_skills.AsReadOnly()));

        public IReadOnlyList<AgentSkill> GetAllSkills() => _skills.AsReadOnly();

        public Task<Result<Skill, string>> ExtractSkillAsync(
            PlanExecutionResult execution, string skillName, string description, CancellationToken ct = default) =>
            Task.FromResult(Result<Skill, string>.Failure("Not supported in simple registry"));
    }
}
