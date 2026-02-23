using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="ProcessLargeInputRequest"/>.
/// Processes large text input using divide-and-conquer parallel processing,
/// falling back to orchestrated generation for smaller inputs.
/// </summary>
public sealed class ProcessLargeInputHandler : IRequestHandler<ProcessLargeInputRequest, string>
{
    private readonly OuroborosAgent _agent;
    private readonly IMediator _mediator;

    public ProcessLargeInputHandler(OuroborosAgent agent, IMediator mediator)
    {
        _agent = agent;
        _mediator = mediator;
    }

    public async Task<string> Handle(ProcessLargeInputRequest request, CancellationToken cancellationToken)
    {
        var models = _agent.ModelsSub;
        var divideAndConquer = models.DivideAndConquer;

        // Use divide-and-conquer if available and input is large enough
        if (divideAndConquer != null && request.LargeInput.Length > 2000)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  [D&C] Processing large input ({request.LargeInput.Length} chars) in parallel...");
            Console.ResetColor();

            var chunks = divideAndConquer.DivideIntoChunks(request.LargeInput);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  [D&C] Split into {chunks.Count} chunks");
            Console.ResetColor();

            var result = await divideAndConquer.ExecuteAsync(request.Task, chunks, cancellationToken);

            return result.Match(
                success =>
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("  [D&C] Parallel processing completed");
                    Console.ResetColor();
                    return success;
                },
                error =>
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  [D&C] Error: {error}");
                    Console.ResetColor();
                    // Fall back to direct orchestration (inline to avoid circular mediator call)
                    return GenerateWithOrchestrationInlineAsync(
                        $"{request.Task}\n\n{request.LargeInput}", cancellationToken).Result;
                });
        }

        // For smaller inputs, use direct orchestration
        return await _mediator.Send(new OrchestrationRequest($"{request.Task}\n\n{request.LargeInput}"), cancellationToken);
    }

    /// <summary>
    /// Inline orchestration fallback used inside the error branch of divide-and-conquer
    /// to avoid a circular mediator Send within a synchronous lambda.
    /// </summary>
    private async Task<string> GenerateWithOrchestrationInlineAsync(string prompt, CancellationToken ct)
    {
        var models = _agent.ModelsSub;

        if (models.OrchestratedModel != null)
        {
            return await models.OrchestratedModel.GenerateTextAsync(prompt, ct);
        }

        if (models.ChatModel != null)
        {
            return await models.ChatModel.GenerateTextAsync(prompt, ct);
        }

        return "[error] No LLM available";
    }
}
