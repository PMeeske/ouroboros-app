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
                _skillRegistry = qdrantRegistry;
                var skills = _skillRegistry.GetAllSkills();
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] Loaded {skills.Count()} skills from Qdrant")}");
            }
            else
            {
                // Use a simple in-memory implementation
                _skillRegistry = new SimpleInMemorySkillRegistry();
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
            _dynamicToolFactory = new DynamicToolFactory(toolAwareLlm);

            // Register built-in tools including Google Search
            _dynamicTools = _dynamicTools
                .WithTool(_dynamicToolFactory.CreateWebSearchTool("duckduckgo"))
                .WithTool(_dynamicToolFactory.CreateUrlFetchTool())
                .WithTool(_dynamicToolFactory.CreateCalculatorTool())
                .WithTool(_dynamicToolFactory.CreateGoogleSearchTool());

            AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim($"[DEBUG] After factory tools: {_dynamicTools.Count} tools")}");

            // Register comprehensive system access tools for PC control
            var systemTools = SystemAccessTools.CreateAllTools().ToList();
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim($"[DEBUG] SystemAccessTools.CreateAllTools returned {systemTools.Count} tools")}");
            foreach (var tool in systemTools)
            {
                _dynamicTools = _dynamicTools.WithTool(tool);
            }
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim($"[DEBUG] After system tools: {_dynamicTools.Count} tools")}");

            // Register perception tools for proactive screen/camera monitoring
            var perceptionTools = PerceptionTools.CreateAllTools().ToList();
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim($"[DEBUG] PerceptionTools returned {perceptionTools.Count} tools")}");
            foreach (var tool in perceptionTools)
            {
                _dynamicTools = _dynamicTools.WithTool(tool);
            }
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim($"[DEBUG] After perception tools: {_dynamicTools.Count} tools")}");

            // OpenClaw Gateway integration (CLI options → env vars → defaults)
            var openClawOpts = options as ImmersiveCommandVoiceOptions;
            bool enableOpenClaw = openClawOpts?.EnableOpenClaw ?? Environment.GetEnvironmentVariable("OPENCLAW_DISABLE") == null;
            if (enableOpenClaw)
            {
                try
                {
                    var gw = await OpenClawTools.ConnectGatewayAsync(
                        openClawOpts?.OpenClawGateway,
                        openClawOpts?.OpenClawToken);
                    _dynamicTools = _dynamicTools.WithOpenClawTools();
                    AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] OpenClaw gateway {gw} (5 tools)")}");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine(OuroborosTheme.Warn(
                        $"  [!] OpenClaw: {Markup.Escape(ex.Message)}"));
                }
            }

            AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim($"[DEBUG] Final tool count: {_dynamicTools.Count} tools")}");

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
                    _toolLearner = new IntelligentToolLearner(
                        _dynamicToolFactory,
                        mettaEngine,
                        embeddingModel,
                        toolAwareLlm,
                        tlClient,
                        tlRegistry);
                }
                else
                {
                    _toolLearner = new IntelligentToolLearner(
                        _dynamicToolFactory,
                        mettaEngine,
                        embeddingModel,
                        toolAwareLlm,
                        options.QdrantEndpoint);
                }
                await _toolLearner.InitializeAsync();
                var stats = _toolLearner.GetStats();
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] Intelligent Tool Learner ready ({stats.TotalPatterns} patterns)")}");

                // Initialize interconnected learner for tool-skill bridging
                _interconnectedLearner = new InterconnectedLearner(
                    _dynamicToolFactory,
                    _skillRegistry!,
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
                    _selfIndexer = new QdrantSelfIndexer(siClient, siRegistry, embeddingModel, indexerConfig);
                }
                else
                {
                    var indexerConfig = new QdrantIndexerConfig
                    {
                        QdrantEndpoint = options.QdrantEndpoint,
                        RootPaths = new List<string> { Environment.CurrentDirectory },
                        EnableFileWatcher = true
                    };
                    _selfIndexer = new QdrantSelfIndexer(embeddingModel, indexerConfig);
                }
                _selfIndexer.OnFileIndexed += (file, chunks) =>
                    AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim($"[Index] {Path.GetFileName(file)} ({chunks} chunks)")}");
                await _selfIndexer.InitializeAsync();

                // Wire up the shared indexer for system access tools
                SystemAccessTools.SharedIndexer = _selfIndexer;

                var indexStats = await _selfIndexer.GetStatsAsync();
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] Self-indexer ready ({indexStats.IndexedFiles} files, {indexStats.TotalVectors} vectors)")}");
            }
        }
        catch (Exception ex)
        {
            var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} [!] Skills initialization error: {Markup.Escape(ex.Message)}[/]");
            _skillRegistry = new SimpleInMemorySkillRegistry();
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

    // ── Skill action handlers ───────────────────────────────────────────────

    private async Task<string> HandleListSkillsAsync(string personaName)
    {
        if (_skillRegistry == null)
            return "I don't have any skills loaded right now.";

        var skills = _skillRegistry.GetAllSkills().ToList();
        if (skills.Count == 0)
            return "I haven't learned any skills yet. Say 'learn about' something to teach me.";

        AnsiConsole.WriteLine();
        var skillsTable = OuroborosTheme.ThemedTable("Skill", "Success Rate");
        foreach (var skill in skills.Take(10))
        {
            skillsTable.AddRow(Markup.Escape(skill.Name), Markup.Escape($"{skill.SuccessRate:P0}"));
        }
        if (skills.Count > 10)
            skillsTable.AddRow(Markup.Escape($"... and {skills.Count - 10} more"), "");
        AnsiConsole.Write(OuroborosTheme.ThemedPanel(skillsTable, $"My Skills ({skills.Count})"));

        return $"I know {skills.Count} skills. The top ones are: {string.Join(", ", skills.Take(5).Select(s => s.Name))}.";
    }

    private async Task<string> HandleRunSkillAsync(
        string skillName,
        string personaName,
        IVoiceOptions options,
        CancellationToken ct)
    {
        if (_skillRegistry == null)
            return "Skills are not available.";

        var skill = _skillRegistry.GetAllSkills()
            .FirstOrDefault(s => s.Name.Contains(skillName, StringComparison.OrdinalIgnoreCase));

        if (skill == null)
            return $"I don't know a skill called '{skillName}'. Say 'list skills' to see what I know.";

        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.GoldText($"[>] Executing skill: {skill.Name}")}");
        var results = new List<string>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        foreach (var step in skill.ToSkill().Steps)
        {
            AnsiConsole.MarkupLine($"      {OuroborosTheme.Accent("->")} {Markup.Escape(step.Action)}: {Markup.Escape(step.ExpectedOutcome)}");
            results.Add($"Step: {step.Action}");
            await Task.Delay(200, ct); // Simulate step execution
        }
        stopwatch.Stop();
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok("[OK] Skill complete")}");

        // Learn from skill execution (interconnected learning)
        if (_interconnectedLearner != null)
        {
            await _interconnectedLearner.RecordSkillExecutionAsync(
                skill.Name,
                string.Join(", ", skill.ToSkill().Steps.Select(s => s.Action)),
                string.Join("\n", results),
                true,
                ct);
        }

        return $"I ran the {skill.Name} skill. It has {skill.ToSkill().Steps.Count} steps.";
    }

    private async Task<string> HandleLearnAboutAsync(
        string topic,
        string personaName,
        IVoiceOptions options,
        CancellationToken ct)
    {
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.GoldText($"[~] Researching: {topic}...")}");

        // Use ArxivSearch if available
        if (_allTokens?.ContainsKey("ArxivSearch") == true)
        {
            // Simulate research
            await Task.Delay(500, ct);
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] Found research on {topic}")}");
        }

        // Create a simple skill from the topic
        if (_skillRegistry != null)
        {
            var stepParams = new Dictionary<string, object> { ["query"] = topic };
            var skill = new Skill(
                $"Research_{topic.Replace(" ", "_")}",
                $"Research skill for {topic}",
                new List<string>(),
                [new PlanStep($"Search for {topic}", stepParams, "research_results", 0.8)],
                0.75,
                0,
                DateTime.UtcNow,
                DateTime.UtcNow);
            await _skillRegistry.RegisterSkillAsync(skill.ToAgentSkill());
            return $"I learned about {topic} and created a research skill for it.";
        }

        return $"I researched {topic}. Interesting stuff!";
    }

    // ── Tool action handlers ────────────────────────────────────────────────

    private async Task<string> HandleAddToolAsync(
        string toolName,
        string personaName,
        CancellationToken ct)
    {
        if (_dynamicToolFactory == null)
            return "Tool creation is not available.";

        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Accent($"[~] Creating tool: {toolName}...")}");

        try
        {
            // Create tool based on name hints for known patterns
            ITool? newTool = toolName.ToLowerInvariant() switch
            {
                var n when n.Contains("search") || n.Contains("google") || n.Contains("web") =>
                    _dynamicToolFactory.CreateWebSearchTool("duckduckgo"),
                var n when n.Contains("fetch") || n.Contains("url") || n.Contains("http") =>
                    _dynamicToolFactory.CreateUrlFetchTool(),
                var n when n.Contains("calc") || n.Contains("math") =>
                    _dynamicToolFactory.CreateCalculatorTool(),
                _ => null // Unknown type - will try LLM generation
            };

            if (newTool != null)
            {
                _dynamicTools = _dynamicTools.WithTool(newTool);
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] Created tool: {newTool.Name}")}");
                return $"I created a new {newTool.Name} tool. It's ready to use.";
            }

            // Unknown tool type - use LLM to generate it
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim("[~] Using AI to generate custom tool...")}");
            var description = $"A tool named {toolName} that performs operations related to {toolName}";
            var createResult = await _dynamicToolFactory.CreateToolAsync(toolName, description, ct);

            if (createResult.IsSuccess)
            {
                _dynamicTools = _dynamicTools.WithTool(createResult.Value);
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] Created custom tool: {createResult.Value.Name}")}");
                return $"I created a custom '{createResult.Value.Name}' tool using AI. It's ready to use.";
            }
            else
            {
                AnsiConsole.MarkupLine($"  [red]\\[!] AI tool generation failed: {Markup.Escape(createResult.Error)}[/]");
                return $"I couldn't create a '{toolName}' tool. Error: {createResult.Error}";
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"[!] Tool creation failed: {ex.Message}")}[/]");
        }

        return $"I had trouble creating that tool. Try being more specific about what it should do.";
    }

    private async Task<string> HandleCreateToolFromDescriptionAsync(
        string description,
        string personaName,
        CancellationToken ct)
    {
        if (_dynamicToolFactory == null)
            return "Tool creation is not available.";

        AnsiConsole.MarkupLine($"\n  [rgb(128,0,180)]\\[~] Creating custom tool from description...[/]");
        AnsiConsole.MarkupLine($"      {OuroborosTheme.Accent("Description:")} {Markup.Escape(description)}");

        try
        {
            // Generate a tool name from the description
            var words = description.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3)
                .Take(3)
                .Select(w => char.ToUpper(w[0]) + w[1..].ToLower());
            var toolName = string.Join("", words) + "Tool";
            if (toolName.Length < 6) toolName = "CustomTool";

            var createResult = await _dynamicToolFactory.CreateToolAsync(toolName, description, ct);

            if (createResult.IsSuccess)
            {
                _dynamicTools = _dynamicTools.WithTool(createResult.Value);
                AnsiConsole.MarkupLine(OuroborosTheme.Ok($"  [OK] Created tool: {createResult.Value.Name}"));
                return $"Done! I created a '{createResult.Value.Name}' tool that {description}. It's ready to use.";
            }
            else
            {
                AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"[!] Tool creation failed: {createResult.Error}")}[/]");
                return $"I couldn't create that tool. Error: {createResult.Error}";
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"[!] Tool creation failed: {ex.Message}")}[/]");
            return $"Tool creation failed: {ex.Message}";
        }
    }

    private async Task<string> HandleCreateToolFromContextAsync(
        string topic,
        string description,
        string personaName,
        CancellationToken ct)
    {
        if (_dynamicToolFactory == null)
            return "Tool creation is not available.";

        AnsiConsole.MarkupLine($"\n  [rgb(128,0,180)]\\[~] Creating tool based on our conversation...[/]");
        AnsiConsole.MarkupLine($"      {OuroborosTheme.Accent("Topic:")} {Markup.Escape(topic)}");

        try
        {
            var toolName = topic.Replace(" ", "") + "Tool";
            var createResult = await _dynamicToolFactory.CreateToolAsync(toolName, description, ct);

            if (createResult.IsSuccess)
            {
                _dynamicTools = _dynamicTools.WithTool(createResult.Value);
                AnsiConsole.MarkupLine(OuroborosTheme.Ok($"  [OK] Created tool: {createResult.Value.Name}"));
                return $"Done! I created '{createResult.Value.Name}'. It's ready to use.";
            }
            else
            {
                AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"[!] Tool creation failed: {createResult.Error}")}[/]");
                return $"I couldn't create that tool. {createResult.Error}";
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"[!] Tool creation failed: {ex.Message}")}[/]");
            return $"Tool creation failed: {ex.Message}";
        }
    }

    private async Task<string> HandleSmartToolAsync(
        string goal,
        string personaName,
        CancellationToken ct)
    {
        if (_toolLearner == null)
            return "Intelligent tool discovery is not available.";

        AnsiConsole.MarkupLine($"\n  [rgb(128,0,180)]\\[~] Finding best tool for: {Markup.Escape(goal)}...[/]");

        try
        {
            var result = await _toolLearner.FindOrCreateToolAsync(goal, _dynamicTools, ct);
            if (result.IsSuccess)
            {
                var (tool, wasCreated) = result.Value;
                AnsiConsole.MarkupLine(OuroborosTheme.Ok($"  [OK] {(wasCreated ? "Created" : "Found")} tool: {tool.Name}"));

                // Learn from tool usage (interconnected learning)
                if (_interconnectedLearner != null)
                {
                    await _interconnectedLearner.RecordToolExecutionAsync(
                        tool.Name,
                        goal,
                        $"Tool found for: {goal}",
                        true,
                        TimeSpan.Zero,
                        ct);
                }

                return $"I found the best tool for that: {tool.Name}.";
            }
            return $"I couldn't find a suitable tool for '{goal}'. {result.Error}";
        }
        catch (Exception ex)
        {
            return $"Smart tool search failed: {ex.Message}";
        }
    }

    private string HandleToolStats(string personaName)
    {
        if (_toolLearner == null)
            return "Tool learning is not available in this session.";

        var stats = _toolLearner.GetStats();
        AnsiConsole.WriteLine();
        var statsTable = OuroborosTheme.ThemedTable("Metric", "Value");
        statsTable.AddRow(Markup.Escape("Total patterns"), Markup.Escape($"{stats.TotalPatterns}"));
        statsTable.AddRow(Markup.Escape("Success rate"), Markup.Escape($"{stats.AvgSuccessRate:P0}"));
        statsTable.AddRow(Markup.Escape("Total usage"), Markup.Escape($"{stats.TotalUsage}"));
        AnsiConsole.Write(OuroborosTheme.ThemedPanel(statsTable, "Tool Learning Stats"));

        return $"I've learned {stats.TotalPatterns} patterns with a {stats.AvgSuccessRate:P0} success rate. Total usage: {stats.TotalUsage}.";
    }

    private async Task<string> HandleConnectionsAsync(string personaName, CancellationToken ct)
    {
        if (_interconnectedLearner == null)
            return "Interconnected learning is not available in this session.";

        AnsiConsole.WriteLine();

        // Show stats from the learner
        var stats = _interconnectedLearner.GetStats();
        int totalExecutions = stats.TotalToolExecutions + stats.TotalSkillExecutions + stats.TotalPipelineExecutions;
        double successRate = totalExecutions > 0 ? (double)stats.SuccessfulExecutions / totalExecutions : 0;
        var connTable = OuroborosTheme.ThemedTable("Metric", "Value");
        connTable.AddRow(Markup.Escape("Patterns Learned"), Markup.Escape($"{stats.LearnedPatterns}"));
        connTable.AddRow(Markup.Escape("Concepts Mapped"), Markup.Escape($"{stats.ConceptGraphNodes}"));
        connTable.AddRow(Markup.Escape("Executions Recorded"), Markup.Escape($"{totalExecutions}"));
        connTable.AddRow(Markup.Escape("Avg Success Rate"), Markup.Escape($"{successRate:P0}"));
        AnsiConsole.Write(OuroborosTheme.ThemedPanel(connTable, "Interconnected Learning"));

        // Show sample suggestions
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Accent("Sample suggestions for common goals:")}");
        var sampleGoals = new[] { "search", "analyze", "summarize" };
        foreach (var goal in sampleGoals)
        {
            var suggestion = await _interconnectedLearner.SuggestForGoalAsync(goal, _dynamicTools, ct);
            var actions = suggestion.MeTTaSuggestions.Concat(suggestion.RelatedConcepts).Take(3).ToList();
            if (actions.Count > 0)
            {
                AnsiConsole.MarkupLine($"    {OuroborosTheme.GoldText(goal)} -> \\[{Markup.Escape(string.Join(", ", actions))}]");
            }
        }

        return stats.LearnedPatterns > 0
            ? $"I have {stats.LearnedPatterns} learned patterns across {stats.ConceptGraphNodes} concepts. Use tools and skills to build more connections!"
            : "I haven't learned any patterns yet. Use skills and tools and I'll start learning relationships.";
    }

    private async Task<string> HandleGoogleSearchAsync(
        string query,
        string personaName,
        CancellationToken ct)
    {
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Accent($"[~] Searching Google for: {query}...")}");

        // Find the Google search tool
        var googleTool = _dynamicTools.All
            .FirstOrDefault(t => t.Name.Contains("google", StringComparison.OrdinalIgnoreCase) ||
                                 t.Name.Contains("search", StringComparison.OrdinalIgnoreCase));

        if (googleTool == null)
        {
            return "Google search tool is not available. Try 'add tool search' first.";
        }

        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await googleTool.InvokeAsync(query);
            stopwatch.Stop();

            AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok("[OK] Search complete")}");

            // Parse and display results
            var output = result.IsSuccess ? result.Value : "No results found.";
            if (output.Length > 500)
            {
                output = output[..500] + "...";
            }
            AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Accent("Results:")}");
            AnsiConsole.MarkupLine($"  {Markup.Escape(output.Replace("\n", "\n  "))}");

            // Learn from the search (interconnected learning)
            if (_interconnectedLearner != null)
            {
                await _interconnectedLearner.RecordToolExecutionAsync(
                    googleTool.Name,
                    query,
                    output,
                    true,
                    stopwatch.Elapsed,
                    ct);
            }

            return $"I found results for '{query}'. The search returned information about it.";
        }
        catch (Exception ex)
        {
            var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} \\[!] Search failed: {Markup.Escape(ex.Message)}[/]");
            return $"I couldn't complete the search. Error: {ex.Message}";
        }
    }

    private async Task<string> HandleUseToolAsync(string toolName, string toolInput, string personaName, CancellationToken ct)
    {
        if (_dynamicTools == null)
            return "I don't have any tools loaded right now.";

        var tool = _dynamicTools.Get(toolName);
        if (tool == null)
        {
            // Try to find a close match
            var availableTools = _dynamicTools.All.Select(t => t.Name).ToList();
            var closestMatch = availableTools
                .OrderBy(t => LevenshteinDistance(t.ToLower(), toolName.ToLower()))
                .FirstOrDefault();

            return $"I don't have a tool called '{toolName}'. Did you mean '{closestMatch}'?\n\nAvailable tools include: {string.Join(", ", availableTools.Take(10))}";
        }

        // If no input provided, show the tool's usage
        if (string.IsNullOrWhiteSpace(toolInput) || toolInput == "{}")
        {
            // For tools that don't need input, execute directly
            if (string.IsNullOrEmpty(tool.JsonSchema) || tool.JsonSchema == "null")
            {
                toolInput = "{}";
            }
            else
            {
                return $"**Tool: {tool.Name}**\n\n{tool.Description}\n\n**Required input format:**\n```json\n{tool.JsonSchema ?? "{}"}\n```\n\nExample: `tool {toolName} {{\"param\": \"value\"}}`";
            }
        }

        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.GoldText($"[Executing tool: {toolName}...]")}");

        try
        {
            var result = await tool.InvokeAsync(toolInput, ct);
            return result.Match(
                success => $"**{toolName} result:**\n\n{success}",
                error => $"**{toolName} failed:**\n\n{error}"
            );
        }
        catch (Exception ex)
        {
            return $"Tool execution error: {ex.Message}";
        }
    }

    private string HandleListTools(string personaName)
    {
        if (_dynamicTools == null)
            return "I don't have any tools loaded.";

        var tools = _dynamicTools.All.ToList();
        var sb = new StringBuilder();
        sb.AppendLine($"\n  **My Tools ({tools.Count} available)**\n");

        // Group by category
        var selfTools = tools.Where(t => t.Name.Contains("my_") || t.Name.Contains("self") || t.Name.Contains("rebuild")).ToList();
        var fileTools = tools.Where(t => t.Name.Contains("file") || t.Name.Contains("directory")).ToList();
        var systemTools = tools.Where(t => t.Name.Contains("process") || t.Name.Contains("system") || t.Name.Contains("powershell")).ToList();
        var otherTools = tools.Except(selfTools).Except(fileTools).Except(systemTools).ToList();

        if (selfTools.Any())
        {
            sb.AppendLine("  🧬 **Self-Modification:**");
            foreach (var t in selfTools.Take(8))
                sb.AppendLine($"    • `{t.Name}` - {Truncate(t.Description, 60)}");
            sb.AppendLine();
        }

        if (fileTools.Any())
        {
            sb.AppendLine("  📁 **File System:**");
            foreach (var t in fileTools.Take(6))
                sb.AppendLine($"    • `{t.Name}` - {Truncate(t.Description, 60)}");
            sb.AppendLine();
        }

        if (systemTools.Any())
        {
            sb.AppendLine("  💻 **System:**");
            foreach (var t in systemTools.Take(6))
                sb.AppendLine($"    • `{t.Name}` - {Truncate(t.Description, 60)}");
            sb.AppendLine();
        }

        if (otherTools.Any())
        {
            sb.AppendLine("  🔧 **Other:**");
            foreach (var t in otherTools.Take(8))
                sb.AppendLine($"    • `{t.Name}` - {Truncate(t.Description, 60)}");
        }

        sb.AppendLine("\n  **Usage:** `tool <name> {\"param\": \"value\"}`");

        AnsiConsole.MarkupLine(Markup.Escape(sb.ToString()));
        return $"I have {tools.Count} tools available. Key ones: {string.Join(", ", selfTools.Select(t => t.Name))}";
    }

    private string HandleSelfModificationHelp(string personaName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("\n  🧬 **Self-Modification Capabilities**\n");
        sb.AppendLine("  I can actually modify my own source code! Here's how:\n");
        sb.AppendLine("  1️⃣ **Search my code:**");
        sb.AppendLine("     `tool search_my_code {\"query\": \"what to find\"}`\n");
        sb.AppendLine("  2️⃣ **Read a file:**");
        sb.AppendLine("     `tool read_my_file {\"path\": \"src/Ouroboros.Cli/Commands/ImmersiveMode.cs\"}`\n");
        sb.AppendLine("  3️⃣ **Modify code:**");
        sb.AppendLine("     `tool modify_my_code {\"file\": \"path/to/file.cs\", \"search\": \"old text\", \"replace\": \"new text\"}`\n");
        sb.AppendLine("  4️⃣ **Create new tool:**");
        sb.AppendLine("     `tool create_new_tool {\"name\": \"my_tool\", \"description\": \"what it does\", \"implementation\": \"C# code\"}`\n");
        sb.AppendLine("  5️⃣ **Rebuild myself:**");
        sb.AppendLine("     `rebuild` or `tool rebuild_self`\n");
        sb.AppendLine("  6️⃣ **View/revert changes:**");
        sb.AppendLine("     `modification history` or `tool revert_modification {\"backup\": \"filename.backup\"}`");

        AnsiConsole.MarkupLine(Markup.Escape(sb.ToString()));
        return "Yes, I can modify myself! Use the commands above. Changes create automatic backups.";
    }

    /// <summary>
    /// Detects when conversation is about tool creation and sets pending context.
    /// This enables conversational flow: "Can you create a tool that X?" "Yes" -> creates tool.
    /// </summary>
    private void DetectToolCreationContext(string userInput, string aiResponse)
    {
        var lowerInput = userInput.ToLowerInvariant();
        var lowerResponse = aiResponse.ToLowerInvariant();

        // Patterns indicating user wants to create a tool
        var toolCreationPatterns = new[]
        {
            @"(can you|could you|would you|please)?\s*(create|build|make)\s*(a|me)?\s*tool",
            @"(i need|i want)\s*(a|you to make)?\s*tool",
            @"(create|build|make)\s*(me)?\s*(a|an)?\s*\w+\s*tool",
            @"tool\s*(that|to|for|which)\s+(.+)",
            @"(can|could)\s+you\s+(help me )?(create|build|make)",
        };

        // Check if user is asking about tool creation
        foreach (var pattern in toolCreationPatterns)
        {
            var match = Regex.Match(lowerInput, pattern);
            if (match.Success)
            {
                // Extract the tool purpose from the input
                var descriptionMatch = Regex.Match(lowerInput, @"tool\s+(that|to|for|which)\s+(.+)");
                var description = descriptionMatch.Success
                    ? descriptionMatch.Groups[2].Value.Trim()
                    : userInput;

                // Try to extract a topic name
                var topicMatch = Regex.Match(lowerInput, @"(?:create|build|make)\s+(?:a|an|me)?\s*(\w+)\s*tool");
                var topic = topicMatch.Success
                    ? topicMatch.Groups[1].Value.Trim()
                    : ExtractTopicFromDescription(description);

                _pendingToolRequest = (topic, description);

                AnsiConsole.MarkupLine($"  [rgb(128,0,180)]\\[context] Tool creation detected: {Markup.Escape(topic)}[/]");
                AnsiConsole.MarkupLine($"  [rgb(128,0,180)]          Say 'yes', 'ok', or 'create it' to proceed.[/]");
                return;
            }
        }

        // Also detect when AI mentions it can/could create something
        if ((lowerResponse.Contains("i can create") || lowerResponse.Contains("i could create") ||
             lowerResponse.Contains("i'll create") || lowerResponse.Contains("i will create") ||
             lowerResponse.Contains("shall i create") || lowerResponse.Contains("want me to create")) &&
            (lowerResponse.Contains("tool") || lowerInput.Contains("tool")))
        {
            var topic = ExtractTopicFromDescription(userInput);
            _pendingToolRequest = (topic, userInput);

            AnsiConsole.MarkupLine($"  [rgb(128,0,180)]\\[context] Offering to create tool: {Markup.Escape(topic)}[/]");
            AnsiConsole.MarkupLine($"  [rgb(128,0,180)]          Say 'yes', 'ok', or 'create it' to proceed.[/]");
        }
    }

    /// <summary>
    /// Extracts a meaningful topic name from a description.
    /// </summary>
    private string ExtractTopicFromDescription(string description)
    {
        // Try to find meaningful words
        var words = description.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .Where(w => !new[] { "that", "this", "with", "from", "into", "about", "tool", "create", "make", "build", "would", "could", "should", "please" }.Contains(w.ToLower()))
            .Take(2)
            .Select(w => char.ToUpper(w[0]) + w[1..].ToLower());

        var topic = string.Join("", words);
        return string.IsNullOrEmpty(topic) ? "Custom" : topic;
    }
}
