using Microsoft.Extensions.Logging;
using Ouroboros.Abstractions.Monads;
using Ouroboros.CLI.Commands;
using Ouroboros.Options;
using Ouroboros.Providers;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Implementation of <see cref="IAskService"/>.
/// The rich <see cref="AskAsync(AskRequest, CancellationToken)"/> overload properly
/// threads all CLI flags through to the pipeline.  The simple string overload is kept
/// for callers that only need a question (ChatCommand, InteractiveCommand).
/// </summary>
public class AskService : IAskService
{
    private static readonly SemaphoreSlim s_agentLock = new(1, 1);
    private readonly ILogger<AskService> _logger;

    public AskService(ILogger<AskService> logger)
    {
        _logger = logger;
    }

    // ── Rich overload ───────────────────────────────────────────────────────

    public async Task<string> AskAsync(AskRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "AskAsync: model={Model} rag={Rag} agent={Agent} router={Router}",
            request.ModelName, request.UseRag, request.AgentMode, request.Router);

        try
        {
            var settings = new ChatRuntimeSettings(
                request.Temperature,
                request.MaxTokens,
                request.TimeoutSeconds,
                request.Stream,
                request.Culture);

            // Agent mode: uses Console.SetOut capture (temporary; full inline migration planned)
            if (request.AgentMode)
            {
                return await RunAgentModeAsync(request, settings);
            }

            // Standard/RAG mode: CreateSemanticCliPipeline returns the answer as a string —
            // no Console.SetOut hack needed.
            var askOpts = ToLegacyOptions(request);
            var pipeline = AskCommands.CreateSemanticCliPipeline(
                withRag:   request.UseRag,
                modelName: request.ModelName,
                embedName: request.EmbedModel,
                k:         request.TopK,
                settings:  settings,
                askOpts:   askOpts);

            var result = await pipeline.Catch().Invoke(request.Question);
            return result.Match(
                success => success,
                error   => $"Error: {error.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AskAsync for question: {Question}", request.Question);
            return $"Error: {ex.Message}";
        }
    }

    // ── Simple overload (delegates to rich overload with defaults) ──────────

    public Task<string> AskAsync(string question, bool useRag = false)
        => AskAsync(new AskRequest(Question: question, UseRag: useRag));

    // ── Private helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Agent mode path: captures the answer via Console.SetOut.
    /// The agent framework writes the final answer to stdout; this captures it cleanly.
    /// </summary>
    private async Task<string> RunAgentModeAsync(AskRequest request, ChatRuntimeSettings settings)
    {
        var askOpts = ToLegacyOptions(request);
        askOpts.Agent = true;

        await s_agentLock.WaitAsync();
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        try
        {
            Console.SetOut(writer);
            await AskCommands.RunAskAsync(askOpts);
            var output = writer.ToString();
            var lines = output.Split('\n')
                .Where(l => !l.StartsWith("[timing]") && !l.StartsWith("[INFO]") && !string.IsNullOrWhiteSpace(l));
            return string.Join("\n", lines);
        }
        finally
        {
            Console.SetOut(originalOut);
            s_agentLock.Release();
        }
    }

    /// <summary>
    /// Maps an <see cref="AskRequest"/> to the legacy <see cref="AskOptions"/> type
    /// required by <see cref="AskCommands"/> static methods.
    /// </summary>
    private static AskOptions ToLegacyOptions(AskRequest r) => new AskOptions
    {
        Question       = r.Question,
        Rag            = r.UseRag,
        Model          = r.ModelName,
        Endpoint       = r.Endpoint,
        ApiKey         = r.ApiKey,
        EndpointType   = r.EndpointType,
        Temperature    = r.Temperature,
        MaxTokens      = r.MaxTokens,
        TimeoutSeconds = r.TimeoutSeconds,
        Stream         = r.Stream,
        Culture        = r.Culture,
        Agent          = r.AgentMode,
        AgentMode      = r.AgentModeType,
        AgentMaxSteps  = r.AgentMaxSteps,
        StrictModel    = r.StrictModel,
        Router         = r.Router,
        CoderModel     = r.CoderModel,
        SummarizeModel = r.SummarizeModel,
        ReasonModel    = r.ReasonModel,
        GeneralModel   = r.GeneralModel,
        Embed          = r.EmbedModel,
        K              = r.TopK,
        Debug          = r.Debug,
        JsonTools      = r.JsonTools,
        Persona        = r.Persona,
        VoiceOnly      = r.VoiceOnly,
        LocalTts       = r.LocalTts,
        VoiceLoop      = r.VoiceLoop,
    };
}
