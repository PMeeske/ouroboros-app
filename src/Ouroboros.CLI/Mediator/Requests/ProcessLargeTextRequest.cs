using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR request to process large text input using divide-and-conquer orchestration.
/// Replaces direct calls to the single-parameter <c>OuroborosAgent.ProcessLargeInputAsync</c>
/// in Commands.cs (distinct from the two-parameter version in Cognition.cs).
/// </summary>
public sealed record ProcessLargeTextRequest(string Input) : IRequest<string>;
