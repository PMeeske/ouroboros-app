using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR command that executes an affect/emotional-state sub-command.
/// </summary>
public sealed record AffectCommandRequest(string SubCommand) : IRequest<string>;
