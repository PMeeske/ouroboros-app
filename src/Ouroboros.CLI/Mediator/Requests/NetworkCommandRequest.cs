using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR command that executes a network status/management sub-command.
/// </summary>
public sealed record NetworkCommandRequest(string SubCommand) : IRequest<string>;
