using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR command that runs the full LLM chat pipeline through the ChatSubsystem.
/// </summary>
public sealed record ChatRequest(string Input) : IRequest<string>;
