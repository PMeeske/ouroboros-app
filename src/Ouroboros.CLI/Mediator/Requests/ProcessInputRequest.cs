using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR query that routes an arbitrary user input string through the agent's
/// command-routing subsystem.
/// </summary>
public sealed record ProcessInputRequest(string Input) : IRequest<string>;
