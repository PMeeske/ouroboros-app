using MediatR;
using Ouroboros.Abstractions.Monads;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="RunPipelineResultRequest"/>.
/// Executes a pipeline DSL expression via PipelineCommands with console output capture.
/// </summary>
public sealed class RunPipelineResultHandler : IRequestHandler<RunPipelineResultRequest, Result<string, string>>
{
    public async Task<Result<string, string>> Handle(RunPipelineResultRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Dsl))
            return Result<string, string>.Failure("Please provide a DSL expression. Example: 'pipeline draft → critique → final'");

        var pipelineOpts = new PipelineOptions
        {
            Dsl = request.Dsl,
            Model = "llama3",
            Temperature = 0.7,
            MaxTokens = 4096,
            TimeoutSeconds = 120,
            Voice = false,
            Culture = Thread.CurrentThread.CurrentCulture.Name,
            Debug = false
        };

        return await CaptureConsoleOutAsync(() => PipelineCommands.RunPipelineAsync(pipelineOpts));
    }

    private static async Task<Result<string, string>> CaptureConsoleOutAsync(Func<Task> action)
    {
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            try
            {
                await action();
                return Result<string, string>.Success(writer.ToString());
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
