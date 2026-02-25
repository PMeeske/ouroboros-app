using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR request that generates text using the agent's orchestrated or chat model.
/// Wraps the logic formerly in <c>OuroborosAgent.GenerateWithOrchestrationAsync</c>.
/// </summary>
public sealed record OrchestrationRequest(string Prompt) : IRequest<string>;
