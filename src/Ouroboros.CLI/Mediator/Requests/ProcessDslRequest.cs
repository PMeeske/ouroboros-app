using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR command that executes a pipeline DSL string through the agent.
/// </summary>
public sealed record ProcessDslRequest(string Dsl) : IRequest<string>;
