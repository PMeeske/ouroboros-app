using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="OrchestrationRequest"/>.
/// Generates text using the agent's orchestrated model (preferred) or chat model (fallback).
/// </summary>
public sealed class OrchestrationHandler : IRequestHandler<OrchestrationRequest, string>
{
    private readonly OuroborosAgent _agent;

    public OrchestrationHandler(OuroborosAgent agent)
    {
        _agent = agent;
    }

    public async Task<string> Handle(OrchestrationRequest request, CancellationToken cancellationToken)
    {
        var models = _agent.ModelsSub;

        if (models.OrchestratedModel != null)
        {
            return await models.OrchestratedModel.GenerateTextAsync(request.Prompt, cancellationToken);
        }

        if (models.ChatModel != null)
        {
            return await models.ChatModel.GenerateTextAsync(request.Prompt, cancellationToken);
        }

        return "[error] No LLM available";
    }
}
