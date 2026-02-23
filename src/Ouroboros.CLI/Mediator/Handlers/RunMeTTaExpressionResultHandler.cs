using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Abstractions.Monads;
using Ouroboros.Application.Tools;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Services;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="RunMeTTaExpressionResultRequest"/>.
/// Executes a MeTTa expression via IMeTTaService with console output capture.
/// </summary>
public sealed class RunMeTTaExpressionResultHandler : IRequestHandler<RunMeTTaExpressionResultRequest, Result<string, string>>
{
    public async Task<Result<string, string>> Handle(RunMeTTaExpressionResultRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Expression))
            return Result<string, string>.Failure("Please provide a MeTTa expression. Example: '!(+ 1 2)' or '(= (greet $x) (Hello $x))'");

        var mettaConfig = new MeTTaConfig(
            Goal: request.Expression,
            Voice: false,
            Culture: Thread.CurrentThread.CurrentCulture.Name,
            Debug: false);

        var mettaService = ServiceContainerFactory.Provider.GetService<IMeTTaService>();
        if (mettaService == null)
            return Result<string, string>.Failure("MeTTa service not available.");

        return await CaptureConsoleOutAsync(() => mettaService.RunAsync(mettaConfig));
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
