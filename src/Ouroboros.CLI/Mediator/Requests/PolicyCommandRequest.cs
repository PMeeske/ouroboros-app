using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR command that executes a policy management sub-command.
/// </summary>
public sealed record PolicyCommandRequest(string SubCommand) : IRequest<string>;
