namespace Ouroboros.ApiHost.Services;

/// <summary>
/// Service for executing AI pipeline operations.
/// Reuses core logic from the CLI implementation.
/// </summary>
public interface IPipelineService
{
    /// <summary>
    /// Executes a question-answer operation with optional RAG (Retrieval Augmented Generation)
    /// </summary>
    /// <param name="request">The ask request containing the question and configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The generated answer text</returns>
    Task<string> AskAsync(AskRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a pipeline defined by DSL (Domain Specific Language)
    /// </summary>
    /// <param name="request">The pipeline request containing the DSL and configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The pipeline execution result</returns>
    Task<string> ExecutePipelineAsync(PipelineRequest request, CancellationToken cancellationToken = default);
}