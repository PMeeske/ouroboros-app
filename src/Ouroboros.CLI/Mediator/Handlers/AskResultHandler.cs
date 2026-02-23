using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Abstractions.Monads;
using Ouroboros.Application.Tools;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Services;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="AskResultRequest"/>.
/// Asks a question via the IAskService resolved from the service container.
/// </summary>
public sealed class AskResultHandler : IRequestHandler<AskResultRequest, Result<string, string>>
{
    private readonly OuroborosAgent _agent;

    public AskResultHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<Result<string, string>> Handle(AskResultRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return Result<string, string>.Failure("What would you like to ask?");

        var askService = ServiceContainerFactory.Provider.GetService<IAskService>();
        if (askService == null)
            return Result<string, string>.Failure("Ask service not available.");

        var config = _agent.Config;
        var answer = await askService.AskAsync(new AskRequest(
            Question: request.Question,
            ModelName: config.Model ?? "llama3",
            Temperature: 0.7,
            MaxTokens: 2048,
            TimeoutSeconds: 120,
            Culture: Thread.CurrentThread.CurrentCulture.Name,
            Debug: config.Debug));
        return Result<string, string>.Success(answer);
    }
}
