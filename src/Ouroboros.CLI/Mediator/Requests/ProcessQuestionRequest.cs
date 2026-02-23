using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR query that asks a question through the agent (uses subsystem routing).
/// </summary>
public sealed record ProcessQuestionRequest(string Question) : IRequest<string>;
