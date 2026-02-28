// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using LangChain.Providers.Ollama;
using Ouroboros.Application.Configuration;
using Ouroboros.Application.Services;
using Ouroboros.Application.Tools;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Options;
using Ouroboros.Providers;
using Spectre.Console;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;
using IEmbeddingModel = Ouroboros.Domain.IEmbeddingModel;

/// <summary>
/// Model subsystem implementation owning all LLM, embedding, and orchestration models.
/// </summary>
public sealed class ModelSubsystem : IModelSubsystem
{
    public string Name => "Models";
    public bool IsInitialized { get; private set; }

    // Core models
    public IChatCompletionModel? ChatModel { get; set; }
    public ToolAwareChatModel? Llm { get; set; }
    public IEmbeddingModel? Embedding { get; set; }

    // Multi-model orchestration
    public OrchestratedChatModel? OrchestratedModel { get; set; }
    public DivideAndConquerOrchestrator? DivideAndConquer { get; set; }

    // Specialized models
    public IChatCompletionModel? CoderModel { get; set; }
    public IChatCompletionModel? ReasonModel { get; set; }
    public IChatCompletionModel? SummarizeModel { get; set; }
    public IChatCompletionModel? VisionChatModel { get; set; }
    public Ouroboros.Core.EmbodiedInteraction.IVisionModel? VisionModel { get; set; }

    // Cost tracking
    public LlmCostTracker? CostTracker { get; set; }

    // Cross-subsystem context (set during InitializeAsync)
    internal SubsystemInitContext Ctx { get; private set; } = null!;

    public void MarkInitialized() => IsInitialized = true;

    /// <inheritdoc/>
    public async Task InitializeAsync(SubsystemInitContext ctx)
    {
        Ctx = ctx;

        // ── LLM ──
        await InitializeLlmCoreAsync(ctx);

        // ── Embedding ──
        await InitializeEmbeddingCoreAsync(ctx);

        MarkInitialized();
    }

    private async Task InitializeLlmCoreAsync(SubsystemInitContext ctx)
    {
        try
        {
            // Avatar mode requires collective intelligence — force it
            var config = ctx.Config;
            if (config.Avatar && !config.CollectiveMode)
            {
                config = config with { CollectiveMode = true };
                ctx.Output.RecordInit("CollectiveMode", true, "forced by --avatar");
            }

            var settings = new ChatRuntimeSettings(config.Temperature, config.MaxTokens, 120, false);
            var (resolvedEndpoint, resolvedApiKey, resolvedEndpointType) = ChatConfig.ResolveWithOverrides(
                config.Endpoint, config.ApiKey, config.EndpointType);
            var endpoint = (resolvedEndpoint ?? config.Endpoint).TrimEnd('/');
            var apiKey = resolvedApiKey;

            CostTracker = new LlmCostTracker(config.Model);

            bool hasPreset = !string.IsNullOrWhiteSpace(config.CollectivePreset);
            if (config.CollectiveMode || hasPreset)
            {
                CollectiveMind mind;

                if (hasPreset)
                {
                    // Multi-model presets (e.g. anthropic-ollama) take priority
                    var multiPreset = MultiModelPresets.GetByName(config.CollectivePreset!);
                    if (multiPreset != null)
                    {
                        mind = CollectiveMindPresetFactory.CreateFromPreset(multiPreset, settings);
                        ctx.Output.RecordInit("Collective Mind", true,
                            $"preset={config.CollectivePreset} ({mind.Pathways.Count} pathways)");
                    }
                    else
                    {
                        // Built-in factory presets: balanced|fast|premium|budget|local|decomposed|single
                        mind = config.CollectivePreset!.ToLowerInvariant() switch
                        {
                            "balanced"   => CollectiveMindFactory.CreateBalanced(settings),
                            "fast"       => CollectiveMindFactory.CreateFast(settings),
                            "premium"    => CollectiveMindFactory.CreatePremium(settings),
                            "budget"     => CollectiveMindFactory.CreateBudget(settings),
                            "local"      => CollectiveMindFactory.CreateLocal(settings: settings),
                            "decomposed" => CollectiveMindFactory.CreateDecomposed(settings),
                            _            => CollectiveMindFactory.CreateFromConfig(
                                                config.Model, config.Endpoint, config.ApiKey,
                                                config.EndpointType, settings),
                        };
                        ctx.Output.RecordInit("Collective Mind", true,
                            $"preset={config.CollectivePreset} ({mind.HealthyPathwayCount} providers)");
                    }
                }
                else
                {
                    mind = CollectiveMindFactory.CreateFromConfig(
                        config.Model, config.Endpoint, config.ApiKey, config.EndpointType, settings);
                    ctx.Output.RecordInit("Collective Mind", true, $"{mind.HealthyPathwayCount} providers");
                }

                // Apply thinking mode from CLI option only if explicitly set (not the default "adaptive").
                // For multi-model presets, CollectiveMindPresetFactory already chose an optimal mode;
                // overriding with the default would silently break preset behaviour.
                bool explicitMode = !string.Equals(config.CollectiveThinkingMode, "adaptive",
                    StringComparison.OrdinalIgnoreCase);
                if (explicitMode)
                {
                    mind.ThinkingMode = config.CollectiveThinkingMode.ToLowerInvariant() switch
                    {
                        "racing"     => CollectiveThinkingMode.Racing,
                        "sequential" => CollectiveThinkingMode.Sequential,
                        "ensemble"   => CollectiveThinkingMode.Ensemble,
                        "decomposed" => CollectiveThinkingMode.Decomposed,
                        _            => CollectiveThinkingMode.Adaptive,
                    };
                }

                ChatModel = mind;
                CostTracker = mind.CostTracker ?? CostTracker;
                return;
            }

            ChatModel = resolvedEndpointType switch
            {
                ChatEndpointType.Anthropic => new AnthropicChatModel(apiKey!, ctx.Config.Model, settings, costTracker: CostTracker),
                ChatEndpointType.OllamaCloud => new OllamaCloudChatModel(endpoint, apiKey ?? "", ctx.Config.Model, settings, costTracker: CostTracker),
                ChatEndpointType.OllamaLocal => new OllamaCloudChatModel(endpoint, "ollama", ctx.Config.Model, settings, costTracker: CostTracker),
                ChatEndpointType.GitHubModels => new GitHubModelsChatModel(apiKey ?? "", ctx.Config.Model, endpoint, settings, costTracker: CostTracker),
                ChatEndpointType.LiteLLM or ChatEndpointType.OpenAI or ChatEndpointType.AzureOpenAI
                    or ChatEndpointType.Groq or ChatEndpointType.Together or ChatEndpointType.Fireworks
                    or ChatEndpointType.Perplexity or ChatEndpointType.DeepSeek or ChatEndpointType.Mistral
                    or ChatEndpointType.Cohere or ChatEndpointType.Google or ChatEndpointType.HuggingFace
                    or ChatEndpointType.Replicate
                    => new LiteLLMChatModel(endpoint, apiKey ?? "", ctx.Config.Model, settings, costTracker: CostTracker),
                ChatEndpointType.OpenAiCompatible => new HttpOpenAiCompatibleChatModel(endpoint, apiKey ?? "", ctx.Config.Model, settings, costTracker: CostTracker),
                _ => endpoint.Contains("localhost", StringComparison.OrdinalIgnoreCase) || endpoint.Contains("127.0.0.1")
                    ? new OllamaCloudChatModel(endpoint, "ollama", ctx.Config.Model, settings, costTracker: CostTracker)
                    : new LiteLLMChatModel(endpoint, apiKey ?? "", ctx.Config.Model, settings, costTracker: CostTracker),
            };

            var label = resolvedEndpointType == ChatEndpointType.Auto
                ? endpoint : resolvedEndpointType.ToString();
            ctx.Output.RecordInit("LLM", true, $"{ctx.Config.Model} @ {label}");

            // Test connection
            var test = await ChatModel.GenerateTextAsync("Respond with just: OK");
            if (string.IsNullOrWhiteSpace(test) || test.Contains("-fallback:"))
                AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  ⚠ LLM: {Markup.Escape(ctx.Config.Model)} (limited mode)"));

            // Multi-model orchestration
            bool isLocal = endpoint.Contains("localhost", StringComparison.OrdinalIgnoreCase) || endpoint.Contains("127.0.0.1");
            await InitializeMultiModelCoreAsync(ctx, settings, endpoint, apiKey, isLocal);
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  ⚠ LLM unavailable: {Markup.Escape(ex.Message)}"));
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  ⚠ LLM unavailable: {Markup.Escape(ex.Message)}"));
        }
    }

    private async Task InitializeMultiModelCoreAsync(
        SubsystemInitContext ctx, ChatRuntimeSettings settings,
        string endpoint, string? apiKey, bool isLocalOllama)
    {
        try
        {
            bool hasSpecialized = !string.IsNullOrEmpty(ctx.Config.CoderModel)
                               || !string.IsNullOrEmpty(ctx.Config.ReasonModel)
                               || !string.IsNullOrEmpty(ctx.Config.SummarizeModel)
                               || !string.IsNullOrEmpty(ctx.Config.VisionModel);
            if (!hasSpecialized || ChatModel == null)
            {
                ctx.Output.RecordInit("Multi-Model", false, "single model mode");
                return;
            }

            IChatCompletionModel CreateModel(string modelName) => isLocalOllama
                ? new OllamaCloudChatModel(endpoint, "ollama", modelName, settings)
                : new HttpOpenAiCompatibleChatModel(endpoint, apiKey ?? "", modelName, settings);

            if (!string.IsNullOrEmpty(ctx.Config.CoderModel))    CoderModel = CreateModel(ctx.Config.CoderModel);
            if (!string.IsNullOrEmpty(ctx.Config.ReasonModel))   ReasonModel = CreateModel(ctx.Config.ReasonModel);
            if (!string.IsNullOrEmpty(ctx.Config.SummarizeModel)) SummarizeModel = CreateModel(ctx.Config.SummarizeModel);
            if (!string.IsNullOrEmpty(ctx.Config.VisionModel))   VisionChatModel = CreateModel(ctx.Config.VisionModel);

            var builder = new OrchestratorBuilder(ctx.Tools.Tools, "general")
                .WithModel("general", ChatModel, ModelType.General,
                    new[] { "conversation", "general-purpose", "versatile", "chat" },
                    maxTokens: ctx.Config.MaxTokens, avgLatencyMs: 1000);

            if (CoderModel != null)
                builder.WithModel("coder", CoderModel, ModelType.Code,
                    new[] { "code", "programming", "debugging", "syntax", "refactor", "implement" },
                    maxTokens: ctx.Config.MaxTokens, avgLatencyMs: 1500);
            if (ReasonModel != null)
                builder.WithModel("reasoner", ReasonModel, ModelType.Reasoning,
                    new[] { "reasoning", "analysis", "logic", "explanation", "planning", "strategy" },
                    maxTokens: ctx.Config.MaxTokens, avgLatencyMs: 1200);
            if (SummarizeModel != null)
                builder.WithModel("summarizer", SummarizeModel, ModelType.General,
                    new[] { "summarize", "condense", "extract", "tldr", "brief" },
                    maxTokens: ctx.Config.MaxTokens, avgLatencyMs: 800);
            if (VisionChatModel != null)
                builder.WithModel("vision", VisionChatModel, ModelType.Analysis,
                    new[] { "vision", "image", "visual", "camera", "see", "look", "photo", "picture", "screenshot" },
                    maxTokens: ctx.Config.MaxTokens, avgLatencyMs: 3000);

            builder.WithMetricTracking(true);
            OrchestratedModel = builder.Build();

            var cnt = 1 + (CoderModel != null ? 1 : 0) + (ReasonModel != null ? 1 : 0)
                        + (SummarizeModel != null ? 1 : 0) + (VisionChatModel != null ? 1 : 0);
            ctx.Output.RecordInit("Multi-Model", true, $"orchestration ({cnt} models)");

            var dcCfg = new DivideAndConquerConfig(
                MaxParallelism: Math.Max(2, Environment.ProcessorCount / 2),
                ChunkSize: 1000, MergeResults: true, MergeSeparator: "\n\n");
            DivideAndConquer = new DivideAndConquerOrchestrator(OrchestratedModel, dcCfg);
            ctx.Output.RecordInit("Divide-and-Conquer", true, $"parallelism={dcCfg.MaxParallelism}");

            await Task.CompletedTask;
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  ⚠ Multi-model orchestration failed: {Markup.Escape(ex.Message)}"));
        }
    }

    private async Task InitializeEmbeddingCoreAsync(SubsystemInitContext ctx)
    {
        var modelsToTry = new[]
        {
            ctx.Config.EmbedModel,
            "mxbai-embed-large", "nomic-embed-text",
            "snowflake-arctic-embed:335m", "all-minilm", "bge-m3",
        }.Distinct().ToArray();

        var embedEndpoint = ctx.Config.EmbedEndpoint.TrimEnd('/');
        var provider = new OllamaProvider(embedEndpoint);

        foreach (var modelName in modelsToTry)
        {
            try
            {
                var embedModel = new OllamaEmbeddingModel(provider, modelName);
                Embedding = new OllamaEmbeddingAdapter(embedModel);
                var testEmbed = await Embedding.CreateEmbeddingsAsync("test");
                ctx.Output.RecordInit("Embeddings", true, $"{modelName} @ {embedEndpoint} (dim={testEmbed.Length})");
                return;
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                if (modelName == ctx.Config.EmbedModel)
                    AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  ⚠ {Markup.Escape(modelName)}: {Markup.Escape(ex.Message.Split('\n')[0])}"));
                Embedding = null;
            }
            catch (InvalidOperationException ex)
            {
                if (modelName == ctx.Config.EmbedModel)
                    AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  ⚠ {Markup.Escape(modelName)}: {Markup.Escape(ex.Message.Split('\n')[0])}"));
                Embedding = null;
            }
        }
        AnsiConsole.MarkupLine(OuroborosTheme.Warn("  ⚠ Embeddings unavailable: No working model found."));
    }

    public IChatCompletionModel? GetEffectiveModel()
        => (IChatCompletionModel?)OrchestratedModel ?? ChatModel;

    public async Task<string> GenerateWithOrchestrationAsync(string prompt, CancellationToken ct = default)
    {
        if (OrchestratedModel != null)
            return await OrchestratedModel.GenerateTextAsync(prompt, ct);

        if (ChatModel != null)
            return await ChatModel.GenerateTextAsync(prompt, ct);

        return "[error] No LLM available";
    }

    public ValueTask DisposeAsync()
    {
        // Models are typically not disposable - they are reused.
        // CostTracker formats on dispose via agent pre-dispose hook, not here.
        IsInitialized = false;
        return ValueTask.CompletedTask;
    }
}
