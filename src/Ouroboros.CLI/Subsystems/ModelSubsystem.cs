// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using LangChain.Providers.Ollama;
using Ouroboros.Application.Services;
using Ouroboros.Application.Tools;
using Ouroboros.Options;
using Ouroboros.Providers;
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
            var settings = new ChatRuntimeSettings(ctx.Config.Temperature, ctx.Config.MaxTokens, 120, false);
            var (resolvedEndpoint, resolvedApiKey, resolvedEndpointType) = ChatConfig.ResolveWithOverrides(
                ctx.Config.Endpoint, ctx.Config.ApiKey, ctx.Config.EndpointType);
            var endpoint = (resolvedEndpoint ?? ctx.Config.Endpoint).TrimEnd('/');
            var apiKey = resolvedApiKey;

            CostTracker = new LlmCostTracker(ctx.Config.Model);

            if (ctx.Config.CollectiveMode)
            {
                var mind = CollectiveMindFactory.CreateFromConfig(
                    ctx.Config.Model, ctx.Config.Endpoint, ctx.Config.ApiKey, ctx.Config.EndpointType, settings);
                ChatModel = mind;
                CostTracker = mind.CostTracker ?? CostTracker;
                ctx.Output.RecordInit("Collective Mind", true, $"{mind.HealthyPathwayCount} providers");
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
                Console.WriteLine($"  \u26a0 LLM: {ctx.Config.Model} (limited mode)");

            // Multi-model orchestration
            bool isLocal = endpoint.Contains("localhost", StringComparison.OrdinalIgnoreCase) || endpoint.Contains("127.0.0.1");
            await InitializeMultiModelCoreAsync(ctx, settings, endpoint, apiKey, isLocal);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  \u26a0 LLM unavailable: {ex.Message}");
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
        catch (Exception ex)
        {
            Console.WriteLine($"  \u26a0 Multi-model orchestration failed: {ex.Message}");
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
            catch (Exception ex)
            {
                if (modelName == ctx.Config.EmbedModel)
                    Console.WriteLine($"  \u26a0 {modelName}: {ex.Message.Split('\n')[0]}");
                Embedding = null;
            }
        }
        Console.WriteLine("  \u26a0 Embeddings unavailable: No working model found.");
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
