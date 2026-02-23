using System.Diagnostics;
using LangChain.Databases;
using LangChain.DocumentLoaders;
using LangChain.Providers.Ollama;
using MediatR;
using Microsoft.Extensions.Logging;
using Ouroboros.Application.Services;
using Ouroboros.CLI.Services;
using Ouroboros.Diagnostics;
using Ouroboros.Providers;
using IEmbeddingModel = Ouroboros.Domain.IEmbeddingModel;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="AskQuery"/>.
/// Inlines the logic from the former <c>AskCommands.CreateSemanticCliPipeline</c>
/// and <c>AskCommands.RunAskAsync</c>, making those static methods obsolete.
/// Supports non-agent (direct pipeline), agent (AgentFactory), and router (multi-model) modes.
/// </summary>
public sealed class AskQueryHandler : IRequestHandler<AskQuery, string>
{
    private readonly ILogger<AskQueryHandler> _logger;

    public AskQueryHandler(ILogger<AskQueryHandler> logger)
    {
        _logger = logger;
    }

    public async Task<string> Handle(AskQuery query, CancellationToken cancellationToken)
    {
        var r = query.Request;

        if (r.Router.Equals("auto", StringComparison.OrdinalIgnoreCase))
            Environment.SetEnvironmentVariable("MONADIC_ROUTER", "auto");
        if (r.Debug)
            Environment.SetEnvironmentVariable("MONADIC_DEBUG", "1");

        var settings = new ChatRuntimeSettings(r.Temperature, r.MaxTokens, r.TimeoutSeconds, r.Stream, r.Culture);

        _logger.LogInformation(
            "AskQueryHandler: model={Model} rag={Rag} agent={Agent} router={Router}",
            r.ModelName, r.UseRag, r.AgentMode, r.Router);

        var sw = Stopwatch.StartNew();
        try
        {
            return r.AgentMode
                ? await HandleAgentModeAsync(r, settings, sw)
                : await HandlePipelineModeAsync(r, settings, sw);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AskQueryHandler failed for question: {Question}", r.Question);
            return $"Error: {ex.Message}";
        }
    }

    // ── Agent mode ─────────────────────────────────────────────────────────────

    private static async Task<string> HandleAgentModeAsync(
        AskRequest r, ChatRuntimeSettings settings, Stopwatch sw)
    {
        var provider = new OllamaProvider();
        (string? endpoint, string? apiKey, ChatEndpointType endpointType) =
            ChatConfig.ResolveWithOverrides(r.Endpoint, r.ApiKey, r.EndpointType);

        IChatCompletionModel chatModel = BuildChatModel(r, provider, endpoint, apiKey, endpointType, settings, isRouter: false);

        var tools = new ToolRegistry();
        if (!tools.All.Any())
        {
            tools = tools
                .WithFunction("echo", "Echo back the input", s => s)
                .WithFunction("uppercase", "Convert text to uppercase", s => s.ToUpperInvariant());
        }

        TrackedVectorStore? ragStore = null;
        IEmbeddingModel? embedModel = null;

        if (r.UseRag)
        {
            var provider2 = new OllamaProvider();
            embedModel = ServiceFactory.CreateEmbeddingModel(endpoint, apiKey, endpointType, r.EmbedModel, provider2);
            ragStore = new TrackedVectorStore();

            string[] seedDocs =
            [
                "Event sourcing captures all changes as immutable events.",
                "Circuit breakers prevent cascading failures in distributed systems.",
                "CQRS separates reads from writes for scalability.",
            ];

            foreach ((string text, int idx) in seedDocs.Select((d, i) => (d, i)))
            {
                try
                {
                    float[] emb = await embedModel.CreateEmbeddingsAsync(text);
                    await ragStore.AddAsync([new Vector { Id = (idx + 1).ToString(), Text = text, Embedding = emb }]);
                }
                catch
                {
                    await ragStore.AddAsync([new Vector { Id = (idx + 1).ToString(), Text = text, Embedding = new float[8] }]);
                }
            }

            if (tools.Get("search") is null && embedModel is not null)
                tools = tools.WithTool(new Ouroboros.Tools.RetrievalTool(ragStore, embedModel));
        }

        var agentInstance = Ouroboros.Agent.AgentFactory.Create(
            r.AgentModeType, chatModel, tools, r.Debug, r.AgentMaxSteps,
            r.UseRag, r.EmbedModel, jsonTools: r.JsonTools, stream: r.Stream);

        string questionForAgent = r.Question;
        if (r.UseRag && ragStore != null && embedModel != null)
        {
            try
            {
                var hits = await ragStore.GetSimilarDocuments(embedModel, r.Question, 3);
                if (hits.Count > 0)
                {
                    string ctx = string.Join("\n- ", hits.Select(h =>
                        h.PageContent.Length > 160 ? h.PageContent[..160] + "..." : h.PageContent));
                    questionForAgent = $"Context:\n- {ctx}\n\nQuestion: {r.Question}";
                }
            }
            catch { /* fallback silently */ }
        }

        string answer = await agentInstance.RunAsync(questionForAgent);
        sw.Stop();
        return answer;
    }

    // ── Standard / RAG pipeline mode ────────────────────────────────────────────

    private static async Task<string> HandlePipelineModeAsync(
        AskRequest r, ChatRuntimeSettings settings, Stopwatch sw)
    {
        var provider = new OllamaProvider();
        (string? endpoint, string? apiKey, ChatEndpointType endpointType) =
            ChatConfig.ResolveWithOverrides(r.Endpoint, r.ApiKey, r.EndpointType);

        IChatCompletionModel chatModel = BuildChatModel(r, provider, endpoint, apiKey, endpointType, settings,
            isRouter: r.Router.Equals("auto", StringComparison.OrdinalIgnoreCase));

        IEmbeddingModel embed = ServiceFactory.CreateEmbeddingModel(endpoint, apiKey, endpointType, r.EmbedModel, provider);

        var tools = new ToolRegistry();
        var llm = new ToolAwareChatModel(chatModel, tools);
        var store = new TrackedVectorStore();

        if (r.UseRag)
        {
            string[] docs =
            [
                "API versioning best practices with backward compatibility",
                "Circuit breaker using Polly in .NET",
                "Event sourcing and CQRS patterns overview"
            ];

            foreach ((string text, int idx) in docs.Select((d, i) => (d, i)))
            {
                try
                {
                    Telemetry.RecordEmbeddingInput([text]);
                    float[] resp = await embed.CreateEmbeddingsAsync(text);
                    await store.AddAsync([new Vector { Id = (idx + 1).ToString(), Text = text, Embedding = resp }]);
                    Telemetry.RecordEmbeddingSuccess(resp.Length);
                    Telemetry.RecordVectors(1);
                }
                catch
                {
                    await store.AddAsync([new Vector { Id = (idx + 1).ToString(), Text = text, Embedding = new float[8] }]);
                    Telemetry.RecordEmbeddingFailure();
                    Telemetry.RecordVectors(1);
                }
            }
        }

        sw.Stop();

        if (!r.UseRag)
        {
            (string text, _) = await llm.GenerateWithToolsAsync(
                $"Answer the following question clearly and concisely.\nQuestion: {r.Question}");
            return text;
        }
        else
        {
            Telemetry.RecordEmbeddingInput([r.Question]);
            float[] qEmb = await embed.CreateEmbeddingsAsync(r.Question);
            Telemetry.RecordEmbeddingSuccess(qEmb.Length);
            var hits = await store.GetSimilarDocumentsAsync(qEmb, r.TopK);
            string ctx = string.Join("\n- ", hits.Select(h => h.PageContent));
            string prompt = $"Use the following context to answer.\nContext:\n- {ctx}\n\nQuestion: {r.Question}";
            (string ragText, _) = await llm.GenerateWithToolsAsync(prompt);
            return ragText;
        }
    }

    // ── Model factory ─────────────────────────────────────────────────────────

    private static IChatCompletionModel BuildChatModel(
        AskRequest r, OllamaProvider provider,
        string? endpoint, string? apiKey, ChatEndpointType endpointType,
        ChatRuntimeSettings settings, bool isRouter)
    {
        if (isRouter)
        {
            var modelMap = new Dictionary<string, IChatCompletionModel>(StringComparer.OrdinalIgnoreCase);

            IChatCompletionModel MakeLocal(string name, string role)
            {
                var m = new OllamaChatModel(provider, name);
                CliModelFactory.ApplyModelPreset(m, name, role);
                return new OllamaChatAdapter(m, settings.Culture);
            }

            string general = r.GeneralModel ?? r.ModelName;
            modelMap["general"] = MakeLocal(general, "general");
            if (!string.IsNullOrWhiteSpace(r.CoderModel))     modelMap["coder"]     = MakeLocal(r.CoderModel!, "coder");
            if (!string.IsNullOrWhiteSpace(r.SummarizeModel)) modelMap["summarize"] = MakeLocal(r.SummarizeModel!, "summarize");
            if (!string.IsNullOrWhiteSpace(r.ReasonModel))    modelMap["reason"]    = MakeLocal(r.ReasonModel!, "reason");

            return new MultiModelRouter(modelMap, fallbackKey: "general");
        }

        if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                return ServiceFactory.CreateRemoteChatModel(endpoint, apiKey, r.ModelName, settings, endpointType);
            }
            catch (Exception ex) when (!r.StrictModel && ex.Message.Contains("Invalid model", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[WARN] Remote model '{r.ModelName}' invalid. Falling back to local 'llama3'. Use --strict-model to disable fallback.");
                var local = new OllamaChatModel(provider, "llama3");
                return new OllamaChatAdapter(local, settings.Culture);
            }
            catch (Exception ex) when (!r.StrictModel)
            {
                Console.WriteLine($"[WARN] Remote model '{r.ModelName}' unavailable ({ex.GetType().Name}). Falling back to local 'llama3'. Use --strict-model to disable fallback.");
                var local = new OllamaChatModel(provider, "llama3");
                return new OllamaChatAdapter(local, settings.Culture);
            }
        }

        // Local Ollama model
        var chat = new OllamaChatModel(provider, r.ModelName);
        CliModelFactory.ApplyModelPreset(chat, r.ModelName);
        return new OllamaChatAdapter(chat, settings.Culture);
    }
}
