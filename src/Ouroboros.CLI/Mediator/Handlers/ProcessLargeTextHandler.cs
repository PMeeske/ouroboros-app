using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="ProcessLargeTextRequest"/>.
/// Processes large text input using divide-and-conquer orchestration, with single-model fallback.
/// </summary>
public sealed class ProcessLargeTextHandler : IRequestHandler<ProcessLargeTextRequest, string>
{
    private readonly OuroborosAgent _agent;

    public ProcessLargeTextHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<string> Handle(ProcessLargeTextRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Input))
            return "Usage: process <large text or file path>";

        // Check if input is a file path
        string textToProcess = request.Input;
        if (File.Exists(request.Input))
        {
            try
            {
                textToProcess = await File.ReadAllTextAsync(request.Input, ct);
            }
            catch (IOException ex)
            {
                return $"Error reading file: {ex.Message}";
            }
        }

        var divideAndConquer = _agent.ModelsSub.DivideAndConquer;
        if (divideAndConquer == null)
        {
            // Fall back to regular processing
            var chatModel = _agent.ModelsSub.ChatModel;
            if (chatModel == null)
                return "No LLM available for processing.";
            return await chatModel.GenerateTextAsync($"Summarize and extract key points:\n\n{textToProcess}");
        }

        try
        {
            var chunks = divideAndConquer.DivideIntoChunks(textToProcess);
            var result = await divideAndConquer.ExecuteAsync(
                "Summarize and extract key points:",
                chunks);

            return result.Match(
                success => $"Processed {chunks.Count} chunks:\n\n{success}",
                error => $"Processing error: {error}");
        }
        catch (InvalidOperationException ex)
        {
            return $"Divide-and-conquer processing failed: {ex.Message}";
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            return $"Divide-and-conquer processing failed: {ex.Message}";
        }
    }
}
